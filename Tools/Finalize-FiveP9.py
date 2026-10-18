"""Seal the final source, model and evidence files after P9's result and handoff exist."""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parent.parent
BASE = ROOT/"Logs/Manager5v5/p9-20261013"
RUN = ROOT/"results/Manager5v5/MNG5_V2-20261013-r001"


def read(path): return json.loads(path.read_text(encoding="utf-8-sig"))
def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda:stream.read(4*1024*1024),b''): digest.update(chunk)
    return digest.hexdigest().upper()


def main():
    assert not (BASE/"completion.json").exists()
    result = read(BASE/"result.json")
    assert result["completed"] and result["evaluationGames"] == 216
    originals = read(ROOT/"Logs/Manager5v5/initial-20261012/original-files.json")
    assert len(originals) == result["originalsPreserved"] == 693
    for row in originals:
        assert sha(ROOT/row["path"]) == row["sha256"].upper(), row["path"]
    for model in result["models"]:
        assert sha(ROOT/model["path"]) == model["sha256"]
        assert sha((ROOT/model["path"]).with_suffix(".pt")) == model["ptSha256"]
    for segment in ("to-100k", "to-200k", "resume-to-200k"):
        saved = BASE/"MNG5_V2-20261013-r001"/segment/"resume-state"
        for row in read(saved/"receipt.json")["files"]:
            assert sha(saved/row["file"]) == row["sha256"].upper()
            if segment == "resume-to-200k":
                assert sha(RUN/"MNG5_Manager_v2"/row["file"]) == row["sha256"].upper()
    report = ROOT/"docs/soccer/rebuild-5v5/23_P9완료_결과와인계.md"
    assert report.is_file() and f"{result['actualStep']:,}개" in report.read_text(encoding='utf-8')
    snapshot = BASE/"p9-source-final.zip"
    assert snapshot.is_file()
    with zipfile.ZipFile(snapshot) as archive:
        source = json.loads(archive.read("source-manifest.json").decode('utf-8-sig'))
        for row in source:
            path = ROOT/row['path']
            assert sha(path) == row['sha256'], row['path']
            assert hashlib.sha256(archive.read(row['path'])).hexdigest().upper() == row['sha256']
    files = {*BASE.rglob('*'), *RUN.rglob('*')}
    for name in ('P9-preflight01', 'P9-preflight02', 'P9-cpu-preflight01'):
        files.update((ROOT/'results/Manager5v5'/name).rglob('*'))
    files = sorted(files)
    manifest = [dict(path=p.relative_to(ROOT).as_posix(), bytes=p.stat().st_size, sha256=sha(p)) for p in files if p.is_file()]
    manifest_path = BASE/"final-files.json"
    assert not manifest_path.exists()
    manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    receipt = dict(completed=True,stage="P9",completedUtc=datetime.now(timezone.utc).isoformat(),
                   resultSha256=sha(BASE/'result.json'),reportSha256=sha(report),sourceArchiveSha256=sha(snapshot),
                   sourceFiles=len(source),evidenceAndModelFiles=len(manifest),fileManifestSha256=sha(manifest_path),
                   actualStep=result['actualStep'],optimizerUpdates=result['optimizerUpdates'],evaluationGames=216,
                   growthDemonstratedOnFixedProtocol=result['growthDemonstratedOnFixedProtocol'],
                   originalsPreserved=result['originalsPreserved'],cudaDriverIssueResolved=False,
                   additionalTrainingStarted=False,p10Completed=False)
    (BASE/'completion.json').write_text(json.dumps(receipt,indent=2),encoding='utf-8')
    print(json.dumps(receipt))


if __name__ == '__main__': main()
