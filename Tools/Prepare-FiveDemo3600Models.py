"""Add verified 3.2M/3.6M policies, preserving the existing eight model assets."""
import json
from pathlib import Path
import numpy as np
import onnx
from onnx.reference import ReferenceEvaluator
from onnx.utils import Extractor
from five_v11_continuation_common import *

def main():
    assert read(BASE/RUN/'training-completion.json')['passed']
    out=ROOT/'Assets/_Soccer/Manager5v5/Demo/Models'
    catalog=read(out/'catalog.json')
    fixtures=read(out/'inference-fixtures.json')
    assert [r['step'] for r in catalog['models']]==list(range(0,2800001,400000))
    for row in catalog['models']:
        assert sha(ROOT/row['asset'])==row['assetSha256']
        assert sha(ROOT/row['source'])==row['sourceSha256']
    for step in [3200000,3600000]:
        assert read(BASE/RUN/f'parity-{step}.json')['passed']
        source=model_dir(RUN)/f'{BEHAVIOR}-{step}.onnx'
        model=onnx.load(source)
        softmax=[n.output[0] for n in model.graph.node if n.op_type=='Softmax']
        assert len(softmax)==1
        extracted=Extractor(model).extract_model(['obs_0','action_masks'],softmax)
        onnx.checker.check_model(extracted)
        target=out/f'stage-{step}.onnx'
        assert not target.exists()
        onnx.save(extracted,target)
        evaluator=ReferenceEvaluator(extracted)
        rng=np.random.RandomState(1710)
        for case in range(3):
            obs=np.zeros((1,676),np.float32) if case==0 else rng.uniform(-1,1,(1,676)).astype(np.float32)
            mask=np.ones((1,8),np.float32)
            if case==2:mask[0,[1,6]]=0
            expected=evaluator.run(None,{'obs_0':obs,'action_masks':mask})[0].reshape(-1)
            assert len(expected)==8 and np.isfinite(expected).all()
            fixtures['cases'].append(dict(step=step,observation=obs[0].tolist(),mask=mask[0].tolist(),probabilities=expected.tolist()))
        catalog['models'].append(dict(step=step,source=source.relative_to(ROOT).as_posix(),sourceSha256=sha(source),
            asset=target.relative_to(ROOT).as_posix(),assetSha256=sha(target),
            purpose='Original trained weights; expose masked Softmax for the demo.'))
    write(out/'catalog.json',catalog)
    write(out/'inference-fixtures.json',fixtures)
    write(BASE/'demo-model-packaging.json',dict(passed=True,models=catalog['models'],fixtures=len(fixtures['cases'])))
    print('PACKAGED',len(catalog['models']))

if __name__=='__main__':main()
