"""Close P9 from completed formal training and the predeclared 216-game comparison."""
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import zipfile
import numpy as np
import yaml

ROOT = Path(__file__).resolve().parent.parent
BASE = ROOT/"Logs/Manager5v5/p9-20261013"
DOC = ROOT/"docs/soccer/rebuild-5v5"
RUN = "MNG5_V2-20261013-r001"
BEHAVIOR = "MNG5_Manager_v2"
CONTRACT = "MNG5-OBS676-A8-R1-M2-v2"


def read(path): return json.loads(path.read_text(encoding="utf-8-sig"))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest().upper()
def write(path, value): path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def main():
    assert not (BASE/"completion.json").exists(), "Preserve prior receipts"
    inputs = read(BASE/"final-inputs.json")
    assert inputs["authorizedBudget"] == 200000
    assert inputs["usageStopRevoked"] is True
    assert inputs["finalSegment"] == "resume-to-200k"
    segments = ("to-100k", "to-200k", inputs["finalSegment"])
    evidence = BASE/RUN
    identity = read(evidence/"identity.json")
    assert identity["contract"] == CONTRACT and identity["device"] == "cpu" and identity["seed"] == 19013
    assert identity["trainerSha256"] == sha(ROOT/"Tools/five_train.py")
    assert identity["configSha256"] == sha(ROOT/"docs/soccer/rebuild-5v5/p9-ppo.yaml")
    payload_root = Path(identity["build"]).parent
    for row in identity["payload"]: assert sha(payload_root/row["path"]) == row["sha256"]
    checks = {}
    for name, expected in (("regression-01", 56), ("editor-01", 11)):
        result = read(BASE/f"{name}.json")
        assert result["exitCode"] == 0 and result["passed"] == expected and result["failed"] == result["skipped"] == 0
        checks[name] = expected
    for file in ("wire-01/smoke-result.json", "rule-wire-02/summary.json", "onnx-wire-01/summary.json",
                 "cpu-preflight-traces.json", "cpu-resume-model-audit.json", "formal-traces.json"):
        assert read(BASE/file)["passed"], file
    # Confirm the actual compiled source against the built archive, not the generic launcher EXE.
    compiled = 0
    with zipfile.ZipFile(BASE/"build-p9-20261013-r1/source-built.zip") as archive:
        for name in archive.namelist():
            if name.startswith("Assets/_Soccer/Manager5v5/") and name.endswith(".cs"):
                assert archive.read(name) == (ROOT/name).read_bytes(), name
                compiled += 1
    originals = read(ROOT/"Logs/Manager5v5/initial-20261012/original-files.json")
    assert len(originals) == 693
    for row in originals: assert sha(ROOT/row["path"]) == row["sha256"].upper(), row["path"]
    preserved_gameplay = 0
    with zipfile.ZipFile(BASE/"before.zip") as archive:
        for name in archive.namelist():
            if name.startswith("Assets/_Soccer/Manager5v5/") and any(f"/{folder}/" in name for folder in ("Runtime", "Profiles", "Prefabs", "UI", "Reference")):
                assert archive.read(name) == (ROOT/name).read_bytes(), name
                preserved_gameplay += 1
    assert sha(ROOT/"ProjectSettings/ProjectSettings.asset") == sha(BASE/"ProjectSettings.before.asset")
    assert sha(ROOT/"ProjectSettings/EditorBuildSettings.asset") == sha(BASE/"EditorBuildSettings.before.asset")
    stages = {}; models = []
    for label, segment in (("initial", "initial"), ("middle", "to-100k"), ("final", inputs["finalSegment"])):
        exit_result = read(evidence/segment/"exit.json")
        process = read(evidence/segment/"process-result.json")
        assert exit_result["exitCode"] == 0 and process["failures"] == []
        parity = read(BASE/f"parity-{label}.json")
        assert parity["passed"]
        step = parity["step"]
        if label != "initial":
            assert read(evidence/segment/"safe-stop.json")["steps"][BEHAVIOR] == step
        if label == "initial": assert step == 0 and process["optimizerUpdates"] == 0 and parity["optimizerSteps"] == []
        elif label == "middle": assert 100000 <= step < 101000
        else: assert 200000 <= step < 201000
        if label != "initial": assert parity["optimizerSteps"] == [process["optimizerUpdates"]]
        model = ROOT/f"results/Manager5v5/{RUN}/{BEHAVIOR}/{BEHAVIOR}-{step}.onnx"
        assert sha(model).lower() == parity["onnxSha256"]
        models.append(dict(label=label, step=step, path=model.relative_to(ROOT).as_posix(), sha256=sha(model),
                           ptSha256=sha(model.with_suffix('.pt')), optimizerUpdates=process["optimizerUpdates"]))
        groups = {}
        for opponent in ("rule", "recover", "initial"):
            folder = BASE/f"evaluation-{label}-{opponent}"
            summary = read(folder/"summary.json"); protocol = read(folder/"protocol.json")
            assert summary["passed"] and summary["games"] == 24
            assert protocol["candidateSha"] == parity["onnxSha256"] and protocol["seeds"] == list(range(29000, 29012))
            assert protocol["evaluatorSha"] == sha(ROOT/"Tools/five_evaluate.py").lower()
            games = [read(p) for p in sorted(folder.glob('match-*/result.json'))]
            assert len(games) == 24 and all(r["passed"] and r["exitCode"] == 0 for r in games)
            assert {(r["seed"], r["candidateTeam"]) for r in games} == {(s,t) for s in range(29000,29012) for t in (0,1)}
            groups[opponent] = dict(summary=summary, games=games)
        stages[label] = dict(step=step, optimizerUpdates=process["optimizerUpdates"], opponents=groups)
    assert len({m["sha256"] for m in models}) == 3, "All learning snapshots are identical"
    exposure = Counter(); restores = []; swaps = []
    pool_audit = ROOT/f"results/Manager5v5/{RUN}/{BEHAVIOR}/five-pool.jsonl"
    for line in pool_audit.read_text().splitlines():
        row = json.loads(line)
        if row["event"] == "learning-exposure": exposure[row["team"]] += row["learnerTransitions"]
        if row["event"] == "restored": restores.append(row)
        if row["event"] == "opponent-swap": swaps.append(row)
    assert sum(exposure.values()) == stages["final"]["step"] and all(exposure[t] > 90000 for t in (0,1))
    stopped = read(evidence/"to-200k/resume-state/receipt.json")
    assert stopped["step"] == 191523 and stopped["optimizerUpdates"] == 2208
    assert {r["step"] for r in restores} >= {0, stages["middle"]["step"], stopped["step"]}
    assert swaps and any(r["poolCount"] == 8 for r in swaps)
    metrics = {}
    for segment in segments:
        assert read(evidence/segment/"exit.json")["exitCode"] == 0
        assert read(evidence/segment/"process-result.json")["failures"] == []
        for line in (evidence/segment/"optimizer.jsonl").read_text().splitlines():
            row = json.loads(line)
            for key, value in row["metrics"].items():
                assert np.isfinite(value)
                metrics.setdefault(key, []).append(value)
    assert len(next(iter(metrics.values()))) == stages["final"]["optimizerUpdates"]
    deltas = {}
    for label in ("middle", "final"):
        pairs = []
        for seed in range(29000,29012):
            values = []
            for opponent in ("rule", "recover", "initial"):
                for team in (0,1):
                    scores = []
                    for stage in ("initial", label):
                        row = next(r for r in stages[stage]["opponents"][opponent]["games"] if r["seed"] == seed and r["candidateTeam"] == team)
                        scores.append(row["score"]["own"]-row["score"]["other"])
                    values.append(scores[1]-scores[0])
            pairs.append(float(np.mean(values)))
        values = np.asarray(pairs)
        rng = np.random.RandomState(91013)
        samples = values[rng.randint(0,12,(10000,12))].mean(axis=1)
        deltas[label] = dict(meanGoalDifferenceGain=float(values.mean()),
                            pairedSeedBootstrap95=np.percentile(samples,[2.5,97.5]).tolist(),
                            independentSeedBlocks=12, pairedGames=72, bootstrapReplicates=10000,
                            statisticalUnit="seed block across both teams and all three opponents")
    growth = deltas["final"]["pairedSeedBootstrap95"][0] > 0
    report = dict(completed=True, p9DevelopmentComplete=True, contract=CONTRACT, runId=RUN,
                  completedUtc=datetime.now(timezone.utc).isoformat(), authorizedBudget=200000,
                  actualStep=stages["final"]["step"], optimizerUpdates=stages["final"]["optimizerUpdates"],
                  trainingSegments=list(segments), usageStopRevoked=True, resumedFromStep=stopped["step"],
                  evaluationGames=216, growthDemonstratedOnFixedProtocol=bool(growth), pairedComparison=deltas,
                  teamLearningExposure=dict(exposure), models=models, unityTests=checks, compiledSourceFiles=compiled,
                  originalsPreserved=len(originals), gameplayFilesPreserved=preserved_gameplay,
                  metrics={k:dict(min=min(v),max=max(v),mean=float(np.mean(v))) for k,v in metrics.items()},
                  stages={label:{opp:data["summary"] for opp,data in stage["opponents"].items()} for label,stage in stages.items()},
                  limitations=["Fixed kickoff; no unseen formation test", "12 independent seed blocks; no universal strength claim",
                               "CUDA native failure preserved; driver issue unresolved; formal run is CPU only",
                               "Stock SDK ELO uses final-transition reward and is excluded from performance claims",
                               "Restart restores model/optimizer/pool/RNG, not in-flight Unity state or partial rollout buffer",
                               "No champion promotion or P10 playable learned-model integration"])
    write(BASE/"result.json", report)
    config = yaml.safe_load((DOC/'p9-ppo.yaml').read_text(encoding='utf-8'))
    environment = read(BASE/'environment.json')
    runtime_hashes = {p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Assets/_Soccer/Manager5v5').rglob('*.cs')}
    profiles = {p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Assets/_Soccer/Manager5v5').rglob('*.asset')}
    write(BASE/'build-payload-manifest.json', identity['payload'])
    registry = dict(schemaVersion=1,status="p9-complete",contract=CONTRACT,behaviorName=BEHAVIOR,
                    lineage="MNG5-v2-from-scratch",currentChampion=None,initialModel=models[0],models=models,
                    trainingStarted=True,automaticResumeAllowed=False,legacy4v4Compatible=False,
                    run=dict(runId=RUN,contract=CONTRACT,parentRun=None,initializationSeed=19013,device="cpu",identityPath=str((evidence/'identity.json').relative_to(ROOT)),
                             identitySha256=sha(evidence/'identity.json'),schemaSha256=sha(DOC/'p8-schema.json'),
                             runtimeSourceHashes=runtime_hashes,profileSha256=sha(ROOT/'Assets/_Soccer/Manager5v5/Profiles/FiveProfile.asset'),assetHashes=profiles,buildSha256=sha(Path(identity['build'])),
                             buildPayloadManifestSha256=sha(BASE/'build-payload-manifest.json'),unityVersion="6000.3.21f1",
                             mlAgentsVersion=dict(unity="4.0.3",python=environment['packages']['mlagents']),pythonVersions=environment,
                             networkConfiguration=config['behaviors'][BEHAVIOR]['network_settings'],trainerConfiguration=config,
                             initialPolicySha256=models[0]['sha256'],
                             optimizerState=dict(path=models[-1]['path'].replace('.onnx','.pt'),sha256=models[-1]['ptSha256'],updates=report['optimizerUpdates']),
                             evaluationProtocol=dict(plan='docs/soccer/rebuild-5v5/22_P9학습과_평가_실행계획.md',games=216,seeds=list(range(29000,29012)),teams=[0,1],opponents=['rule','T7 전체 공 회수','initial policy']),
                             stopCriteria=['200000 learning-team manager decisions','runtime, numeric, resource or provenance failure'],
                             usageStopRevoked=True, resumedFromStep=stopped['step'], trainingSegments=list(segments),
                             actualStep=report['actualStep'],optimizerUpdates=report['optimizerUpdates'],budget=200000,
                             authorization="User approved 200,000 manager decisions, then explicitly revoked usage-limit stopping and requested completion and evaluation",
                             resultPath=str((BASE/'result.json').relative_to(ROOT))),
                    excludedRuns=["P9-preflight01", "P9-preflight02", "P9-cpu-preflight01"])
    write(DOC/"p9-model-registry.json",registry)
    print(json.dumps({k:report[k] for k in ('completed','actualStep','optimizerUpdates','evaluationGames','growthDemonstratedOnFixedProtocol','pairedComparison')}))


if __name__ == "__main__": main()
