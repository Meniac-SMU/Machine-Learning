"""Seal the selected rule opponent only after independent matches and regression checks pass."""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import zipfile

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/"Logs/Manager5v5/rule-strong-20261013"

def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def sha(p):
    h=hashlib.sha256()
    with p.open("rb") as f:
        for block in iter(lambda:f.read(4*1024*1024),b""):h.update(block)
    return h.hexdigest().upper()

def main():
    assert not (BASE/"completion.json").exists()
    assessment=read(BASE/"holdout-assessment.json")
    assert assessment["passed"] and assessment["games"]==208
    tests=[]
    for name in ("strong-v7-edit","strong-v7-play","strong-v7-native-holdout"):
        r=read(BASE/(name+".json"));assert r["exitCode"]==0 and r["testResult"]=="Passed" and r["failed"]==0
        tests.append(dict(name=name,passed=r["passed"],receiptSha256=sha(BASE/(name+".json"))))
    originals=read(ROOT/"Logs/Manager5v5/initial-20261012/original-files.json")
    assert len(originals)==693
    for r in originals:assert sha(ROOT/r["path"])==r["sha256"].upper(),r["path"]
    historical=read(ROOT/"Logs/Manager5v5/p9-20261013/final-files.json")
    for r in historical:assert sha(ROOT/r["path"])==r["sha256"].upper(),r["path"]
    with zipfile.ZipFile(BASE/"before.zip") as before:
        prefix="Assets/_Soccer/Manager5v5/Runtime/"
        preserved=[p for p in before.namelist() if p.startswith(prefix) and p.endswith(".cs") and not p.endswith(("FiveRuleManager.cs","FiveTechniques.cs"))]
        for p in preserved:assert before.read(p)==(ROOT/p).read_bytes(),p
        techniques=prefix+"FiveTechniques.cs"
        original=before.read(techniques).decode("utf-8-sig").replace("\r\n","\n")
        expected=original.replace("Physics.RaycastAll(origin,offset.normalized,offset.magnitude,~0,QueryTriggerInteraction.Ignore)","RuleSceneRaycasts(origin,offset.normalized,offset.magnitude,~0,QueryTriggerInteraction.Ignore)")
        assert (ROOT/techniques).read_text(encoding="utf-8-sig")==expected, "Unexpected player technique change"
        def legacy(text):return text[text.index("    public static class FiveRuleManager"):text.index("    public sealed partial class FiveMatch")].replace("\r\n","\n")
        assert legacy(before.read(prefix+"FiveRuleManager.cs").decode("utf-8-sig"))==legacy((ROOT/(prefix+"FiveRuleManager.cs")).read_text(encoding="utf-8-sig"))
    built=BASE/"build-rule-strong-v7-final-20261013/source-built.zip"
    with zipfile.ZipFile(built) as archive:
        assets=[p for p in archive.namelist() if p.startswith("Assets/_Soccer/Manager5v5/")]
        compiled=[p for p in assets if p.endswith(".cs")]
        for p in assets:assert archive.read(p)==(ROOT/p).read_bytes(),p
    assert read(BASE/"scene-regeneration-verification.json")["semanticGraphIdentical"]
    for case in (BASE/"holdout-v7").glob("*/protocol.json"):
        p=read(case);assert p["ruleSettingsSource"] is None and p["ruleSettingsSha"] is None
        assert read(case.parent/"verification.json")["passed"]
        for row in read(case.parent/"payload-after.json"):
            assert sha(Path(p["build"]).parent/row["path"])==row["sha256"].upper(),row["path"]
    for name in ("ProjectSettings","EditorBuildSettings"):
        assert sha(ROOT/"ProjectSettings"/(name+".asset"))==sha(BASE/(name+".before.asset"))
    report=ROOT/"docs/soccer/rebuild-5v5/26_강한규칙형감독_완료검증과인계.md"
    registry=ROOT/"docs/soccer/rebuild-5v5/rule-opponent-registry.json"
    assert report.is_file() and read(registry)["status"]=="validated"
    snapshot=BASE/"source-final.zip"
    with zipfile.ZipFile(snapshot) as archive:
        source=json.loads(archive.read("source-manifest.json").decode("utf-8-sig"))
        for row in source:
            assert sha(ROOT/row["path"])==row["sha256"].upper(),row["path"]
            assert hashlib.sha256(archive.read(row["path"])).hexdigest().upper()==row["sha256"].upper()
    manifest=[dict(path=p.relative_to(ROOT).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(BASE.rglob("*")) if p.is_file()]
    (BASE/"final-files.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding="utf-8")
    receipt=dict(completed=True,ruleVersion="strong-v7",completedUtc=datetime.now(timezone.utc).isoformat(),
        holdoutGames=208,assessmentSha256=sha(BASE/"holdout-assessment.json"),tests=tests,
        originalsPreserved=len(originals),historicalP9FilesPreserved=len(historical),
        unchangedSharedRuntimeFiles=len(preserved),legacySelectorPreserved=True,
        compiledSourceFilesMatched=len(compiled),compiledAssetFilesMatched=len(assets),finalSourceFiles=len(source),
        sourceArchiveSha256=sha(snapshot),reportSha256=sha(report),registrySha256=sha(registry),
        fileManifestSha256=sha(BASE/"final-files.json"),evidenceFiles=len(manifest),
        additionalTrainingStarted=False,p10Completed=False)
    (BASE/"completion.json").write_text(json.dumps(receipt,indent=2),encoding="utf-8")
    print(json.dumps(receipt,indent=2))

if __name__=="__main__":main()
