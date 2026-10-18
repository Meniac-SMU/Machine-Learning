"""Frozen P9 policies, paired sampling seeds and full 300-second Player matches."""
import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
import hashlib
import json
from pathlib import Path
import re
import traceback

import numpy as np
import onnx
from onnx.reference import ReferenceEvaluator
from onnx.utils import Extractor
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel

ROOT = Path(__file__).resolve().parent.parent
CONTRACT = "MNG5-OBS676-A8-R1-M2-v2"
COMMANDS = ["T1 속공 전개", "T2 전방 롱패스", "T3 와이드 탈압박", "T4 측면 컷백",
            "T5 진로 차단 수비", "T6 수신자 봉쇄", "T7 전체 공 회수", "T8 골문 앞 집결"]


def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def write(path, value): path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


class FrozenPolicy:
    def __init__(self, source, seed):
        self.rng = np.random.RandomState(seed)
        self.evaluator = None
        if source != "recover":
            model = onnx.load(source)
            onnx.checker.check_model(model)
            obs = next(x for x in model.graph.input if x.name == "obs_0")
            assert obs.type.tensor_type.shape.dim[1].dim_value == 676
            softmax = [n.output[0] for n in model.graph.node if n.op_type == "Softmax"]
            assert len(softmax) == 1, "Expected one categorical branch"
            self.evaluator = ReferenceEvaluator(Extractor(model).extract_model(["obs_0", "action_masks"], softmax))

    def act(self, observation):
        if self.evaluator:
            p = self.evaluator.run(None, {"obs_0": observation[None].astype(np.float32),
                                         "action_masks": np.ones((1, 8), np.float32)})[0][0].astype(float)
            assert p.shape == (8,) and np.isfinite(p).all() and (p >= 0).all() and abs(p.sum()-1) < 1e-5
            p /= p.sum()
            return int(self.rng.choice(8, p=p)), p
        return 6, np.eye(8)[6]


def game(args, index, seed, team):
    folder = args.output / f"match-{index:03d}"
    folder.mkdir()
    env = None
    report = dict(passed=False, seed=seed, candidateTeam=team, candidate=args.candidate, opponent=args.opponent,
                  contract=CONTRACT, commandNames=COMMANDS)
    terminal_rows = {}
    totals = {}
    counts = np.zeros((3, 8), dtype=np.int64)
    probabilities = np.zeros((3, 8))
    states = np.zeros(3, dtype=np.int64)
    try:
        policies = {team: FrozenPolicy(args.candidate, seed+100000)}
        if args.opponent != "rule": policies[1-team] = FrozenPolicy(args.opponent, seed+200000)
        channel = EngineConfigurationChannel()
        channel.set_configuration_parameters(time_scale=100, quality_level=0, target_frame_rate=-1, capture_frame_rate=0)
        extra = ["--five-p9-trace-dir", str(folder)]
        if args.opponent == "rule": extra += ["--five-rule-team", str(1-team)]
        env = UnityEnvironment(file_name=str(args.build), seed=seed, worker_id=args.worker+index,
                               no_graphics=True, timeout_wait=90, side_channels=[channel],
                               log_folder=str(folder), additional_args=extra)
        env.reset()
        names = {int(name.rsplit("=", 1)[1]): name for name in env.behavior_specs}
        assert set(names) == set(policies), "Rule opponent was not isolated from the communicator"
        for name, spec in env.behavior_specs.items():
            assert name.startswith("MNG5_Manager_v2?team=")
            assert len(spec.observation_specs) == 1 and spec.observation_specs[0].shape == (676,)
            assert spec.action_spec.continuous_size == 0 and list(spec.action_spec.discrete_branches) == [8]
            totals[name] = 0.0
        sample_observations = []
        for _ in range(1000):
            for side, name in names.items():
                decision, terminal = env.get_steps(name)
                for batch in (decision, terminal):
                    assert np.isfinite(batch.obs[0]).all() and (np.abs(batch.obs[0]) <= 1.00001).all()
                if len(terminal):
                    assert len(terminal) == 1 and not terminal.interrupted[0]
                    totals[name] += float(terminal.reward[0])
                    obs = terminal.obs[0][0]
                    assert obs[20] == 1 and obs[17] == 0, "Not a full-match terminal"
                    own, other = [round(float(x/(1-x))) for x in obs[15:17]]
                    assert totals[name] == own-other, "Reward does not equal the score difference"
                    terminal_rows[side] = dict(own=own, other=other, reward=totals[name])
                if side in terminal_rows: continue
                if len(decision):
                    assert len(decision) == 1
                    assert decision.action_mask is None or not any(m.any() for m in decision.action_mask)
                    totals[name] += float(decision.reward[0])
                    obs = decision.obs[0][0]
                    action, p = policies[side].act(obs)
                    if side == team:
                        state = 0 if obs[5:10].sum() > .5 else 1 if obs[10:15].sum() > .5 else 2
                        counts[state, action] += 1; probabilities[state] += p; states[state] += 1
                        if len(sample_observations) < 16: sample_observations.append(obs.copy())
                    env.set_actions(name, ActionTuple(discrete=np.array([[action]], dtype=np.int32)))
            if len(terminal_rows) == len(names): break
            env.step()
        assert len(terminal_rows) == len(names), "Terminal timed out"
        process = env._process
        env.close(); env = None
        exit_code = process.poll()
        assert exit_code == 0, f"Player exit {exit_code}"
        for log in folder.glob("Player*.log"):
            assert not re.search(r"^(?:[\w.]*Exception:|Assertion failed|\[MNG FATAL\])", log.read_text(errors="replace"), re.M), "Player error log"
        trace_files = list(folder.glob("trace-*.jsonl"))
        assert len(trace_files) == 1
        boundary = 0; terminals = []; reset_count = 0; rule_actions = []
        with trace_files[0].open(encoding="utf-8-sig") as stream:
            for line in stream:
                row = json.loads(line)
                assert row["contract"] == CONTRACT
                assert row["ruleOpponentTeam"] == (1-team if args.opponent == "rule" else -1)
                boundary = max(boundary, row["boundaryCorrections"])
                if row["kind"] == "reset": reset_count += 1
                if row["kind"] == "terminal": terminals.append(row)
                if row["kind"] == "decision" and row["team"] != team: rule_actions.append(row["action"])
        assert boundary == 0, "Physics boundary correction occurred"
        assert len(terminals) == 2 and all(r["elapsed"] >= 300 for r in terminals)
        assert sum(r["reward"] for r in terminals) == 0
        score = terminal_rows[team]
        for row in terminals:
            assert row["reward"] == (row["redScore"]-row["navyScore"])*(1 if row["team"] == 0 else -1)
        np.save(folder/"observations.npy", np.asarray(sample_observations))
        report.update(passed=True, exitCode=exit_code, score=score, boundaryCorrections=boundary,
                      stateOrder=["own", "opponent", "neutral"], stateCounts=states.tolist(),
                      actionCounts=counts.tolist(), probabilitySums=probabilities.tolist(),
                      opponentActionCounts=np.bincount(rule_actions, minlength=8).tolist(),
                      unityTerminalCount=len(terminals), resets=reset_count)
    except Exception:
        report["error"] = traceback.format_exc()
        raise
    finally:
        if env is not None: env.close()
        write(folder/"result.json", report)
    return report


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--candidate", required=True)
    parser.add_argument("--opponent", required=True)
    parser.add_argument("--build", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--pairs", type=int, default=12)
    parser.add_argument("--seed", type=int, default=29000)
    parser.add_argument("--workers", type=int, default=4)
    parser.add_argument("--worker", type=int, default=80)
    args = parser.parse_args()
    args.build = (ROOT/args.build).resolve(); args.output = (ROOT/args.output).resolve()
    assert args.build.is_relative_to(ROOT) and args.output.is_relative_to(ROOT)
    assert 1 <= args.workers <= 4 and 1 <= args.pairs <= 32
    for key in ("candidate", "opponent"):
        value = getattr(args, key)
        if value not in ("rule", "recover"):
            value = (ROOT/value).resolve(); assert value.is_relative_to(ROOT) and value.is_file()
            setattr(args, key, str(value))
    assert args.candidate != "rule"
    args.output.mkdir(parents=True, exist_ok=False)
    write(args.output/"protocol.json", dict(contract=CONTRACT, candidate=args.candidate, opponent=args.opponent,
          candidateSha=None if args.candidate == "recover" else sha(args.candidate),
          opponentSha=None if args.opponent in ("rule", "recover") else sha(args.opponent),
          seeds=list(range(args.seed, args.seed+args.pairs)), candidateTeams=[0, 1], seconds=300,
          sampling="categorical, independent RNG per policy and match, identical seeds across checkpoints",
          limitations="Standard kickoff only; held-out sampling trajectories, not unseen formations.",
          evaluatorSha=sha(__file__), commandNames=COMMANDS))
    (args.output/"evaluator-used.py").write_bytes(Path(__file__).read_bytes())
    rows = []
    with ThreadPoolExecutor(max_workers=args.workers) as executor:
        futures = [executor.submit(game, args, 2*i+team, args.seed+i, team)
                   for i in range(args.pairs) for team in (0, 1)]
        for future in as_completed(futures):
            rows.append(future.result())
            print(f"MATCH {len(rows)}/{2*args.pairs}", flush=True)
    rows.sort(key=lambda r:(r["seed"], r["candidateTeam"]))
    differences = [r["score"]["own"]-r["score"]["other"] for r in rows]
    write(args.output/"summary.json", dict(passed=all(r["passed"] for r in rows), games=len(rows),
          wins=sum(x>0 for x in differences), draws=sum(x==0 for x in differences), losses=sum(x<0 for x in differences),
          goalsFor=sum(r["score"]["own"] for r in rows), goalsAgainst=sum(r["score"]["other"] for r in rows),
          meanGoalDifference=float(np.mean(differences)),
          actionCounts=np.sum([r["actionCounts"] for r in rows], axis=0).tolist(),
          stateCounts=np.sum([r["stateCounts"] for r in rows], axis=0).tolist(),
          probabilitySums=np.sum([r["probabilitySums"] for r in rows], axis=0).tolist()))


if __name__ == "__main__": main()
