"""Close P8 only after real Unity/Player gates and source-preservation checks pass."""
import hashlib
import json
from pathlib import Path
import re
import zipfile
from datetime import datetime, timezone
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
BASE = ROOT / "Logs/Manager5v5/p8-r1m2-20261013"


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    decision = read(ROOT / "docs/soccer/rebuild-5v5/p8-contract-decision.json")
    require(decision["status"] == "confirmed", "User requested reward/memory design first; P8 cannot be finalized yet.")
    require(decision["selectedReward"] == "R1" and decision["selectedMemory"] == "M2", "Confirmed design differs from this implementation.")
    configuration = read(BASE / "final-inputs.json")
    source_archive = BASE / configuration.get("sourceArchive", "p8-source-final.zip")
    require(not (BASE / "completion.json").exists(), "Preserve the existing completion receipt.")
    checks = {}
    for name, count in configuration["tests"].items():
        run = read(BASE / f"{name}.json")
        xml = ET.parse(BASE / f"{name}.xml").getroot()
        require(run["exitCode"] == 0 and run["testResult"] == "Passed" and run["passed"] == count
                and run["failed"] == 0 and run["skipped"] == 0, f"Failed test run: {name}")
        require(xml.attrib["result"] == "Passed" and int(xml.attrib["passed"]) == count, "XML disagrees with the test receipt.")
        checks[name] = {"passed": count, "xmlSha256": sha(BASE / f"{name}.xml")}
    build_dir = BASE / configuration["build"]
    build = read(build_dir / "build-result.json")
    require(build["exitCode"] == 0 and Path(build["executable"]).is_file(), "Player build missing.")
    smoke = read(BASE / configuration["wire"] / "smoke-result.json")
    require(smoke["passed"] and smoke["exitCode"] == 0 and smoke["optimizerUpdates"] == 0 and not smoke["trainingStarted"], "LLAPI smoke failed.")
    require(smoke["verifierSha256"] == sha(ROOT / "Tools/Smoke-FiveLearning.py")
            == sha(BASE / configuration["wire"] / "verifier-used.py"), "Wire verifier provenance mismatch.")
    require(smoke["executableSha256"].upper() == sha(Path(build["executable"])), "Tested Player hash mismatch.")
    # Unity's .exe is a launcher and can stay identical after managed-code changes.
    # Preserve the complete deployed payload, including both MNG5 assemblies and the scene.
    payload_root = Path(build["executable"]).parent
    payload = [{"path": p.relative_to(payload_root).as_posix(), "sha256": sha(p), "bytes": p.stat().st_size}
               for p in sorted(payload_root.rglob("*")) if p.is_file()
               and p.relative_to(payload_root).as_posix() != "Soccer5v5Learning_Data/ML-Agents/Timers/FiveLearning_timers.json"]
    require(any(r["path"].endswith("/MNG5.Learning.dll") for r in payload), "Learning assembly is missing from the Player.")
    # Build-Lifecycle -Action Use appends its usage receipt only after a successful
    # test. Preserve/hash that bookkeeping in the final manifest, but compare the
    # deployed program separately from this post-test audit record.
    program_payload = [r for r in payload if r["path"] != "build-usage.jsonl"]
    wire_payload = [r for r in read(BASE / configuration["wire"] / "payload-manifest.json") if r["path"] != "build-usage.jsonl"]
    require(program_payload == wire_payload, "Deployed payload differs from the actual wire test.")
    payload_manifest = BASE / "build-manifest.json"
    payload_manifest.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    schema = read(build_dir / "learning-schema.json")
    require(schema["observationSize"] == 676 and len(schema["featureNames"]) == 676
            and len(set(schema["featureNames"])) == 676 and schema["discreteBranches"] == [8]
            and not schema["actionMasks"] and not schema["existingModelAllowed"], "Schema mismatch.")
    require(schema["contract"] == decision["selectedContract"] == smoke["contract"]
            and schema["frameSize"] == 168 and schema["historyFrames"] == 4
            and schema["historyOffsetsSeconds"] == [0, .25, .5, .75]
            and schema["goalReward"] == 1 and schema["concededReward"] == -1
            and schema["otherReward"] == schema["terminalBonus"] == 0, "R1+M2 schema mismatch.")
    require(read(ROOT / "docs/soccer/rebuild-5v5/p8-schema.json") == schema, "Published schema differs from the built schema.")
    originals = read(ROOT / "Logs/Manager5v5/initial-20261012/original-files.json")
    changed = [r["path"] for r in originals if sha(ROOT / r["path"]) != r["sha256"].upper()]
    require(len(originals) == 693 and not changed, "Original preservation failed: " + str(changed))
    preserved = []
    with zipfile.ZipFile(ROOT / "Logs/Manager5v5/p8-20261013/p8-before.zip") as before:
        for path in before.namelist():
            if path.startswith("Assets/_Soccer/Manager5v5/Runtime/") or path in (
                "Assets/_Soccer/Manager5v5/Prefabs/FiveArena.prefab", "Assets/_Soccer/Manager5v5/Scenes/FiveRuleMatch.unity",
                "Assets/_Soccer/Manager5v5/Profiles/FiveProfile.asset"):
                require(before.read(path) == (ROOT / path).read_bytes(), "P7 gameplay changed: " + path)
                preserved.append(path)
    for filename in ("ProjectSettings", "EditorBuildSettings"):
        require(sha(ROOT / f"ProjectSettings/{filename}.asset") == sha(BASE / f"{filename}.before.asset"), filename + " drift.")
    # Every current gameplay/learning source must match both the built source and final tests.
    runtime_paths = sorted([p for folder in ("Runtime", "Learning") for p in (ROOT / "Assets/_Soccer/Manager5v5" / folder).glob("*") if p.suffix in (".cs", ".asmdef")])
    source_hashes = {p.relative_to(ROOT).as_posix(): sha(p) for p in runtime_paths}
    validation_paths = [p for folder in ("Editor", "Tests") for p in (ROOT / "Assets/_Soccer/Manager5v5" / folder).rglob("*")
                        if p.suffix in (".cs", ".asmdef")]
    validation_hashes = {p.relative_to(ROOT).as_posix(): sha(p) for p in validation_paths}
    archives = [build_dir / "source-built.zip", source_archive]
    archives += [Path(read(BASE / f"{name}.json")["sourceArchive"]) for name in configuration["tests"]]
    for path in archives:
        with zipfile.ZipFile(path) as archive:
            for relative, expected in {**source_hashes, **validation_hashes}.items():
                require(hashlib.sha256(archive.read(relative)).hexdigest().upper() == expected, "Runtime differs in " + str(path) + ": " + relative)
    for document in (ROOT / "docs/soccer/rebuild-5v5/18_P8학습계약과_검증계획.md", ROOT / "docs/soccer/rebuild-5v5/21_P8완료_검증과인계.md"):
        for target in re.findall(r"\]\(([^)]+)\)", document.read_text(encoding="utf-8")):
            if not target.startswith(("https://", "http://", "#")):
                resolved = (document.parent / target.split("#")[0]).resolve()
                require(resolved.exists() or resolved == BASE / "completion.json", "Broken documentation link: " + target)
    report = {"completedUtc": datetime.now(timezone.utc).isoformat(), "completedThrough": "P8", "passed": True,
              "contract": schema["contract"], "tests": checks, "wire": smoke, "build": build,
              "buildFileCount": len(payload), "buildManifestSha256": sha(payload_manifest),
              "originalFilesPreserved": len(originals), "p7FilesPreserved": len(preserved),
              "runtimeSourceHashes": source_hashes, "sourceArchive": str(source_archive),
              "validationSourceHashes": validation_hashes, "decision": decision,
              "sourceArchiveSha256": sha(source_archive), "trainingStarted": False,
              "programPayloadFileCount": len(program_payload),
              "postTestBookkeeping": [r for r in payload if r["path"] == "build-usage.jsonl"],
              "optimizerUpdates": 0, "p9Authorized": False, "visualReview": "P7 unchanged; P8 headless interface validation only"}
    (BASE / "completion.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"passed": True, "completedThrough": "P8", "tests": checks, "originalFilesPreserved": len(originals)}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
