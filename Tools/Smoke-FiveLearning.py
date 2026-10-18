"""Exercise the P8 Player through LLAPI only. No trainer, torch or optimizer is imported."""
import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path
import traceback

import numpy as np
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel

ROOT = Path(__file__).resolve().parent.parent
RUNTIME_TIMER = "Soccer5v5Learning_Data/ML-Agents/Timers/FiveLearning_timers.json"


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def payload_manifest(directory):
    return [{"path": p.relative_to(directory).as_posix(), "sha256": hashlib.sha256(p.read_bytes()).hexdigest().upper(),
             "bytes": p.stat().st_size} for p in sorted(directory.rglob("*"))
            if p.is_file() and p.relative_to(directory).as_posix() != RUNTIME_TIMER]


def check_history(observation):
    require(observation.shape == (676,), "Expected four 168-value frames and four validity flags.")
    flags = observation[672:]
    require(flags[0] == 1 and np.isin(flags, [0, 1]).all(), "Invalid history flags.")
    require(np.all(flags[1:] <= flags[:-1]), "History must fill newest first.")
    for frame, valid in enumerate(flags):
        if not valid:
            require((observation[frame * 168:(frame + 1) * 168] == 0).all(), "Missing frame is not zero padded.")


def inside(relative):
    path = (ROOT / relative).resolve()
    require(path.is_relative_to(ROOT), "Evidence and Player must stay in the project.")
    return path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--build", required=True)
    parser.add_argument("--evidence", required=True)
    parser.add_argument("--worker", type=int, default=28)
    args = parser.parse_args()
    executable = inside(args.build)
    evidence = inside(args.evidence)
    evidence.mkdir(parents=True, exist_ok=False)
    verifier_bytes = Path(__file__).read_bytes()
    (evidence / "verifier-used.py").write_bytes(verifier_bytes)
    report = {"passed": False, "trainingStarted": False, "optimizerUpdates": 0, "contract": "MNG5-OBS676-A8-R1-M2-v2",
              "executable": str(executable), "executableSha256": hashlib.sha256(executable.read_bytes()).hexdigest(),
              "verifierSha256": hashlib.sha256(verifier_bytes).hexdigest().upper()}
    payload = payload_manifest(executable.parent)
    (evidence / "payload-manifest.json").write_text(json.dumps(payload, indent=2), encoding="utf-8")
    env = None
    try:
        channel = EngineConfigurationChannel()
        channel.set_configuration_parameters(time_scale=100, quality_level=0, target_frame_rate=-1, capture_frame_rate=0)
        env = UnityEnvironment(file_name=str(executable), seed=18013, worker_id=args.worker, no_graphics=True,
                               timeout_wait=60, side_channels=[channel], log_folder=str(evidence),
                               additional_args=["--five-p8-trace", str(evidence / "trace.jsonl")])
        env.reset()
        require(len(env.behavior_specs) == 2, "Two team-qualified behaviors are required.")
        behaviors = sorted(env.behavior_specs)
        for name, spec in env.behavior_specs.items():
            require(name.startswith("MNG5_Manager_v2?team="), "Old or unexpected behavior name.")
            require(len(spec.observation_specs) == 1 and spec.observation_specs[0].shape == (676,), "Observation ABI drift.")
            require(spec.action_spec.continuous_size == 0 and list(spec.action_spec.discrete_branches) == [8], "Action ABI drift.")
        # A second explicit reset must restart the shared world once, not once per team.
        env.reset()
        rewards = defaultdict(float)
        terminals = []
        episode_decisions = Counter()
        episode_numbers = Counter()
        counts = {name: Counter() for name in behaviors}
        decision_rows = 0
        for step in range(320):
            for team, name in enumerate(behaviors):
                decision, terminal = env.get_steps(name)
                for batch in (decision, terminal):
                    require(np.isfinite(batch.obs[0]).all(), "Non-finite observation over the wire.")
                    require((np.abs(batch.obs[0]) <= 1.00001).all(), "Observation normalization drift.")
                    for observation in batch.obs[0]:
                        check_history(observation)
                if decision.action_mask is not None:
                    require(not any(np.asarray(mask).any() for mask in decision.action_mask), "An action was masked.")
                for i, agent_id in enumerate(terminal.agent_id):
                    key = (name, int(agent_id))
                    # SDK 4.0.3 keeps agent IDs across episodes. A terminal and the next
                    # initial decision may also be delivered in the same exchange.
                    require(episode_decisions[key] > 0, "Terminal without a new episode's decisions.")
                    total = rewards[key] + float(terminal.reward[i])
                    observation = terminal.obs[0][i]
                    own = round(float(observation[15] / (1 - observation[15])))
                    opponent = round(float(observation[16] / (1 - observation[16])))
                    require(abs(total - (own - opponent)) < .0001, "Communicator reward disagrees with the final score.")
                    require(observation[20] == 1 and observation[17] == 0, "Terminal observation is stale.")
                    require(not terminal.interrupted[i], "A 300-second game is a true game terminal.")
                    episode_numbers[key] += 1
                    terminals.append({"behavior": name, "agentId": int(agent_id), "episode": episode_numbers[key], "ownScore": own,
                                      "opponentScore": opponent, "reward": total})
                    rewards[key] = 0
                    episode_decisions[key] = 0
                for i, agent_id in enumerate(decision.agent_id):
                    key = (name, int(agent_id))
                    rewards[key] += float(decision.reward[i])
                    episode_decisions[key] += 1
                actions = np.zeros((len(decision), 1), dtype=np.int32)
                for i in range(len(decision)):
                    action = (step + team * 3) % 8
                    actions[i, 0] = action
                    counts[name][action] += 1
                    decision_rows += 1
                env.set_actions(name, ActionTuple(discrete=actions))
            if all(sum(t["behavior"] == name for t in terminals) >= 2 for name in behaviors):
                break
            env.step()
        require(all(sum(t["behavior"] == name for t in terminals) >= 2 for name in behaviors), "Two full matches did not finish for both teams.")
        require(all(set(counts[name]) == set(range(8)) for name in behaviors), "Not all eight actions crossed the communicator.")
        report.update(behaviors=behaviors, decisions=decision_rows, terminals=terminals,
                      actionCounts={name: dict(counts[name]) for name in behaviors})
        process = env._process
        env.close()
        env = None
        report["exitCode"] = process.poll()
        require(report["exitCode"] == 0, "Player did not exit normally.")
        require(payload == payload_manifest(executable.parent), "Player payload changed during the smoke test.")
        timer = executable.parent / RUNTIME_TIMER
        if timer.exists():
            (evidence / "runtime-timers.json").write_bytes(timer.read_bytes())
            report["runtimeDiagnostic"] = {"path": RUNTIME_TIMER, "sha256": hashlib.sha256(timer.read_bytes()).hexdigest().upper()}
        traces = [json.loads(line) for line in (evidence / "trace.jsonl").read_text(encoding="utf-8-sig").splitlines()]
        trace_terminals = [r for r in traces if r["kind"] == "terminal"]
        require(len(trace_terminals) >= 4, "Unity terminal trace missing.")
        for row in trace_terminals:
            require(row["elapsed"] >= 300 and row["reward"] == (row["redScore"] - row["navyScore"]) * (1 if row["team"] == 0 else -1), "Unity terminal receipt mismatch.")
        resets = [r for r in traces if r["kind"] == "reset"]
        require([r["episode"] for r in resets] == list(range(1, len(resets) + 1)), "Duplicate/missing shared reset sequence.")
        first_decisions = {}
        actual_actions = defaultdict(set)
        full_history_rows = 0
        maximum_sample_error = 0
        for row in traces:
            if row.get("observation"):
                require(row["contract"] == report["contract"], "Trace ABI mismatch.")
                observation = np.asarray(row["observation"])
                check_history(observation)
                require(len(row["sampleTimes"]) == 4 and len(row["sampleErrors"]) == 4, "Missing sampling audit.")
                full_history_rows += int(all(observation[672:] == 1))
                for frame, valid in enumerate(observation[672:]):
                    sample, error = row["sampleTimes"][frame], row["sampleErrors"][frame]
                    if valid:
                        require(0 <= error <= .0201 and abs(row["elapsed"] - frame * .25 - sample - error) < .0001,
                                "History sample is future/stale or timing evidence disagrees.")
                        maximum_sample_error = max(maximum_sample_error, error)
                    else:
                        require(sample == -1 and error == -1, "Invalid frame needs explicit missing timestamps.")
            if row["kind"] == "decision":
                first_decisions.setdefault((row["episode"], row["team"]), row)
                actual_actions[row["team"]].add(row["action"])
        require(all(row["elapsed"] == 0 for row in first_decisions.values()), "Physics advanced before the first action after reset.")
        require(all(row["observation"][672:] == [1, 0, 0, 0] for row in first_decisions.values()), "Prior match history leaked after reset.")
        require(full_history_rows > 100, "Short history never became available during real matches.")
        require(all(actual_actions[team] == set(range(8)) for team in (0, 1)), "All eight actions must reach Unity, not just Python.")
        report.update(passed=True, unityTerminalCount=len(trace_terminals), sharedResets=len(resets),
                      observationSize=676, fullHistoryRows=full_history_rows, maximumSampleErrorSeconds=maximum_sample_error,
                      payloadFileCount=len(payload), payloadPreserved=True)
    except Exception:
        report["error"] = traceback.format_exc()
        raise
    finally:
        if env is not None:
            env.close()
        (evidence / "smoke-result.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
