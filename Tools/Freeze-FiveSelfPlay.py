"""Verify and register the tested player/rule source and built payload without changing them."""
import hashlib
import json
from pathlib import Path
import zipfile

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/p9-selfplay-20261014'
BUILD=ROOT/'Builds/Manager5v5/p9-selfplay-frozen-20261014'

def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(4*1024*1024),b''):h.update(b)
    return h.hexdigest()


def main():
    out=BASE/'frozen-version.json'; assert not out.exists()
    tests=[]
    for name in ('cutback-contract-06','full-regression-07','edit-regression-08'):
        r=read(BASE/(name+'.json'));assert r['exitCode']==0 and r['testResult']=='Passed' and r['failed']==0
        tests.append(dict(name=name,passed=r['passed'],sha256=sha(BASE/(name+'.json'))))
    build=read(BASE/'build-p9-selfplay-frozen-20261014/build-result.json'); assert build['exitCode']==0
    source=[]
    with zipfile.ZipFile(BASE/'build-p9-selfplay-frozen-20261014/source-built.zip') as archive:
        for row in json.loads(archive.read('source-manifest.json')):
            if row['path'].startswith('Assets/_Soccer/Manager5v5/'):
                assert sha(ROOT/row['path']).lower()==row['sha256'].lower(), row['path']
                source.append(row)
    changed=[]
    with zipfile.ZipFile(BASE/'before-source.zip') as archive:
        for row in json.loads(archive.read('source-manifest.json')):
            if row['path'].startswith('Assets/_Soccer/Manager5v5/') and sha(ROOT/row['path']).lower()!=row['sha256'].lower():
                changed.append(row['path'])
        for name in ('FiveStrongRule.cs','FiveRuleSettings.cs','FiveRuleForecast.cs','FiveLearningContract.cs'):
            p='Assets/_Soccer/Manager5v5/Runtime/'+name
            assert archive.read(p)==(ROOT/p).read_bytes(), p
    preserved={}
    for name,path in [('originals','Logs/Manager5v5/initial-20261012/original-files.json'),('priorP9','Logs/Manager5v5/p9-20261013/final-files.json')]:
        rows=read(ROOT/path)
        for row in rows:assert sha(ROOT/row['path']).lower()==row['sha256'].lower(), row['path']
        preserved[name]=len(rows)
    for path in ('ProjectSettings/ProjectSettings.asset','ProjectSettings/EditorBuildSettings.asset','ProjectSettings/ProjectVersion.txt','Packages/manifest.json','Packages/packages-lock.json'):
        assert (ROOT/path).read_bytes()==(BASE/Path(path).name).read_bytes(), path
    payload=[dict(path=p.relative_to(ROOT).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(BUILD.rglob('*'))
             if p.is_file() and p.name not in ('build-usage.jsonl','FiveLearning_timers.json')]
    result=dict(passed=True,playerRuntime='attack-keeper-v2-reception',ruleSelector='strong-v11',
        contract='MNG5-OBS676-A8-R1-M2-v2',reward='score +1 / conceded -1 / all others 0',
        tests=tests,source=source,payload=payload,preserved=preserved,changedFromTaskStart=changed,
        executableSha256=sha(BUILD/'Soccer5v5Learning.exe'),
        note='Original pressured fixture receives then is tackled; separate mirrored uncontested fixtures require reception and shooting. No forced success.',
        preflightPending=True)
    out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(passed=True,preserved=preserved,changed=changed,executableSha256=result['executableSha256']),ensure_ascii=False))


if __name__=='__main__':main()
