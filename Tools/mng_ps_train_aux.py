"""Append-only PS4 objective revision after documented 100k forgetting."""
import argparse
import json
from pathlib import Path
import sys
import mng_ps_train as experiment
import mng_ps_transport as transport
from mng_ps_auxiliary import install, DATA


def main():
    p=argparse.ArgumentParser();p.add_argument('--additional',type=int,required=True);a=p.parse_args()
    assert a.additional in (200000,300000,400000)
    e=experiment.EVIDENCE
    gate=e/f'auxiliary-gate-{a.additional}.json'
    assert not gate.exists()
    assert experiment.sha(DATA)=='e8d02c202815dbc7db9ba48ff17819d36d41a454bc57726f00fadf674f46366e'
    source=Path(__file__).with_name('mng_ps_auxiliary.py')
    gate.write_text(json.dumps(dict(target=a.additional,reason='100k validation positive-pass probability fell from .2393 to .00436',
        objective='existing PPO loss + 0.1 * (successful-demo CE + 2 * parent KL on stratified negative examples)',
        trainOnly=True,actionOverride=False,maskChanges=False,rewardChanges=False,criticAuxiliaryGradient=False,
        datasetSha=experiment.sha(DATA),sourceSha=experiment.sha(source),launcherSha=experiment.sha(__file__),
        checkpointBeforeSha=experiment.sha(experiment.RESULT/'MNG_ManagerV2/checkpoint.pt'),
        sampling='numpy RandomState(929501 + persisted Adam update count); train split only',
        counters='256 auxiliary examples per existing Adam update; zero extra environment steps'),indent=2))
    for f in (source,Path(__file__)):
        (e/'source'/f'{a.additional}-{f.name}').write_bytes(f.read_bytes())
    install(e/f'auxiliary-updates-{a.additional}.jsonl')
    sys.argv=[str(Path(transport.__file__)),'--additional',str(a.additional)]
    transport.main()


if __name__=='__main__':main()
