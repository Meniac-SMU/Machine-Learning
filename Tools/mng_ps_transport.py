"""Audited PS launch overlay: only replace the local TCP base port.

Original prepared source hashes remain immutable and are still checked by train().
Worker indexes and Python learner seeds are unchanged. Unity ALSO uses the port as
a spawn-seed offset, so training spawn seeds change; this is explicitly recorded.
Policy, optimizer and pool continue from the saved checkpoint.
"""
import argparse
import json
from pathlib import Path
import socket
import shutil
import sys
import mng_ps_train as experiment
import mng_v2_formal_learn as formal


def main():
    p=argparse.ArgumentParser();p.add_argument('--additional',type=int,required=True);a=p.parse_args()
    handles=[]
    try:
        for port in range(12500,12532):
            s=socket.socket();s.bind(('localhost',port));handles.append(s)
    finally:
        for s in handles:s.close()
    e=experiment.EVIDENCE
    gate=dict(passed=True,scope='Transport-only launch argument override',basePortBefore=6500,basePortAfter=12500,
        additionalTarget=a.additional,workers=32,seed=929401,workerIndexes=list(range(32)),
        unitySpawnOffsetBefore=list(range(6500,6532)),unitySpawnOffsetAfter=list(range(12500,12532)),
        unitySpawnSeedsChanged=True,
        originalManifestSha=experiment.sha(e/'manifest.json'),overlaySha=experiment.sha(__file__),
        checkpointBeforeSha=experiment.sha(experiment.RESULT/'MNG_ManagerV2/checkpoint.pt'),
        poolBeforeSha=experiment.sha(experiment.RESULT/'MNG_ManagerV2/mng-v2-pool.pt'),
        reason='Windows denies bind to localhost:6506; 12500..12531 verified free; no system setting changed',
        seedEvidence='Installed learn.py create_environment_factory uses seed + worker_id, independent of start_port')
    gate_path=e/f'transport-gate-{a.additional}.json'
    assert not gate_path.exists();gate_path.write_text(json.dumps(gate,indent=2))
    (e/'source'/f'transport-overlay-{a.additional}.py').write_bytes(Path(__file__).read_bytes())
    original=formal.main
    def launch():
        index=sys.argv.index('--base-port')+1
        assert sys.argv[index]=='6500'
        sys.argv[index]='12500'
        return original()
    formal.main=launch
    experiment.train(a.additional)
    source=experiment.RESULT/'run_logs'
    target=e/f'PS-to-{a.additional}'/'run_logs'
    shutil.copytree(source,target)


if __name__=='__main__':main()
