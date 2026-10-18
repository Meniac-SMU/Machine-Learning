"""Freeze a tested execution candidate; retain all earlier artifacts and verify lineage."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile

ROOT=Path(__file__).resolve().parent.parent
BASE=ROOT/'Logs/Manager5v5/p9-tactics-20261014'
BUILD=ROOT/'Builds/Manager5v5/p9-tactics-v3-20261014'
def read(path):return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def sha(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda:stream.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest()
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--play',required=True);parser.add_argument('--edit',required=True)
    args=parser.parse_args();out=BASE/'frozen-version.json';assert not out.exists()
    tests=[]
    for name in (args.play,args.edit):
        result=read(BASE/(name+'.json'))
        assert result['exitCode']==0 and result['testResult']=='Passed' and result['failed']==0 and result['skipped']==0
        tests.append(dict(name=name,passed=result['passed'],sha256=sha(BASE/(name+'.json'))))
    build=read(BASE/'build-p9-tactics-v3-20261014/build-result.json');assert build['exitCode']==0
    source=[]
    with zipfile.ZipFile(BASE/'build-p9-tactics-v3-20261014/source-built.zip') as archive:
        for item in json.loads(archive.read('source-manifest.json')):
            if item['path'].startswith('Assets/_Soccer/Manager5v5/'):
                assert sha(ROOT/item['path']).lower()==item['sha256'].lower(),item['path']
                source.append(item)
    with zipfile.ZipFile(BASE/'before-source.zip') as archive:
        # Selection/rewards/physical abilities are not altered to manufacture the usage target.
        for name in ('FiveStrongRule.cs','FiveRuleSettings.cs','FiveLearningContract.cs','FiveProfile.cs','FivePlayer.cs','FiveTechniques.cs'):
            path='Assets/_Soccer/Manager5v5/Runtime/'+name
            assert archive.read(path)==(ROOT/path).read_bytes(),path
    preserved={}
    for label,path in [('originals','Logs/Manager5v5/initial-20261012/original-files.json'),
                       ('earlyP9','Logs/Manager5v5/p9-20261013/final-files.json')]:
        items=read(ROOT/path)
        for item in items:assert sha(ROOT/item['path']).lower()==item['sha256'].lower(),item['path']
        preserved[label]=len(items)
    prior=read(ROOT/'Logs/Manager5v5/p9-selfplay-20261014/frozen-version.json')
    for item in prior['payload']:assert sha(ROOT/item['path']).lower()==item['sha256'].lower(),item['path']
    preserved['previousBuildFiles']=len(prior['payload'])
    old_models=read(ROOT/'Logs/Manager5v5/p9-selfplay-20261014/MNG5_V2-20261014-r001/model-registry.json')['stages']
    for item in old_models:
        assert sha(item['onnx'])==item['onnxSha256']
        assert sha(Path(item['onnx']).with_suffix('.pt'))==item['ptSha256']
    preserved['previousSelfPlayModels']=len(old_models)
    for path in ('ProjectSettings/ProjectSettings.asset','ProjectSettings/EditorBuildSettings.asset','ProjectSettings/ProjectVersion.txt','Packages/manifest.json','Packages/packages-lock.json'):
        assert (ROOT/path).read_bytes()==(BASE/Path(path).name).read_bytes(),path
    payload=[dict(path=p.relative_to(ROOT).as_posix(),bytes=p.stat().st_size,sha256=sha(p))
             for p in sorted(BUILD.rglob('*')) if p.is_file() and p.name not in ('build-usage.jsonl','FiveLearning_timers.json')]
    names=['Run-FiveTacticSelfPlay600k.py','Run-FiveSelfPlayTraining.ps1','five_selfplay_train.py','five_trace_guard.py',
           'five_tactic_evaluate.py','five_evaluation_audit.py','five_match_metrics.py','Audit-FiveCheckpoint.py','mng_player_log_guard.py']
    execution=[dict(path='Tools/'+name,sha256=sha(ROOT/'Tools'/name)) for name in names]
    execution.append(dict(path='docs/soccer/rebuild-5v5/p9-selfplay-1200k.yaml',sha256=sha(ROOT/'docs/soccer/rebuild-5v5/p9-selfplay-1200k.yaml')))
    result=dict(passed=True,playerRuntime='tactics-v3-expansion',ruleSelector='strong-v11',contract='MNG5-OBS676-A8-R1-M2-v2',
                reward='score +1 / conceded -1 / all others 0',tests=tests,source=source,payload=payload,
                executionFiles=execution,preserved=preserved,authorizedStop=600000,eventualGoal=1200000,
                selectionTarget=dict(minimum=.015,maximum=.5,forcedSelection=False),
                preflightPending=True,quality='Not established by regression or build success.')
    out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(passed=True,tests=tests,preserved=preserved,sourceFiles=len(source),payloadFiles=len(payload)),ensure_ascii=False))
if __name__=='__main__':main()
