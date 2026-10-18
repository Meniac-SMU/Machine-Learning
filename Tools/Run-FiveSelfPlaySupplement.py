"""Predeclared read-only matches after the full 1.2M primary run completes."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time
from five_evaluation_audit import audit

ROOT=Path(__file__).resolve().parent.parent
RUN='MNG5_V2-20261014-r001'
EVIDENCE=ROOT/'Logs/Manager5v5/p9-selfplay-20261014'
BASE=EVIDENCE/RUN
OUT=BASE/'supplemental'


def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def write(p,v):Path(p).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')


def main():
    assert read(BASE/'orchestrator-status.json')['state']=='complete'
    registry=read(BASE/'model-registry.json')
    final=next(r for r in registry['stages'] if r['step']==1200000)
    assert sha(final['onnx'])==final['onnxSha256']
    old=next(r for r in read(ROOT/'docs/soccer/rebuild-5v5/p9-model-registry.json')['models'] if r['label']=='final')
    assert old['step']==200011 and sha(ROOT/old['path']).lower()==old['sha256'].lower()
    OUT.mkdir(exist_ok=True)
    pairs=[('final-vs-recover',final['onnx'],'recover'),('recover-vs-strong','recover','rule'),
           ('final-vs-old-p9',final['onnx'],str(ROOT/old['path']))]
    env=os.environ.copy();env.update(OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',MKL_NUM_THREADS='1',CUDA_VISIBLE_DEVICES='-1')
    results=[]
    for label,candidate,opponent in pairs:
        for item in read(EVIDENCE/'execution-file-manifest.json'):
            assert sha(ROOT/item['path'])==item['sha256']
        receipt=OUT/(label+'-process.json')
        output=OUT/label
        write(OUT/'status.json',dict(state='running',operation=label,completed=results))
        if not receipt.exists():
            print('START',label,flush=True)
            command=[sys.executable,'Tools/five_selfplay_evaluate.py','--candidate',candidate,'--opponent',opponent,
                     '--build','Builds/Manager5v5/p9-selfplay-frozen-20261014/Soccer5v5Learning.exe',
                     '--output',str(output),'--pairs','12','--seed','59000','--workers','24','--worker','160']
            with (OUT/(label+'-stdout.log')).open('w') as stdout,(OUT/(label+'-stderr.log')).open('w') as stderr:
                code=subprocess.call(command,cwd=ROOT,env=env,stdout=stdout,stderr=stderr)
            write(receipt,dict(exitCode=code,arguments=command,ended=time.time()))
        assert read(receipt)['exitCode']==0
        summary=read(output/'summary.json');assert summary['passed'] and summary['games']==24
        checked=audit(output);assert checked['passed']
        if candidate=='recover':
            counts=[sum(s[i] for s in summary['actionCounts']) for i in range(8)]
            assert sum(counts)==counts[6] and counts[6]>0
        if opponent=='recover':
            counts=checked['opponentActionCounts']
            assert sum(counts)==counts[6] and counts[6]>0
        results.append(dict(label=label,summary=summary,audit=checked))
        print('DONE',label,flush=True)
    write(OUT/'status.json',dict(state='complete',completed=results))


if __name__=='__main__':main()
