"""Package the eight authorized checkpoints without modifying training artifacts."""
import hashlib
import json
from pathlib import Path
import numpy as np
import onnx
from onnx.reference import ReferenceEvaluator
from onnx.utils import Extractor

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Assets/_Soccer/Manager5v5/Demo/Models'
OUT.mkdir(parents=True, exist_ok=True)
rows = []
fixtures = []
for step in range(0, 2800001, 400000):
    version = 6 if step == 2800000 else 5
    run = f'MNG5_V{version}-20261016-r00{2 if version == 6 else 1}'
    source = ROOT / f'results/Manager5v5/{run}/MNG5_Manager_v{version}/MNG5_Manager_v{version}-{step}.onnx'
    model = onnx.load(source)
    softmax = [n.output[0] for n in model.graph.node if n.op_type == 'Softmax']
    assert len(softmax) == 1, softmax
    extracted = Extractor(model).extract_model(['obs_0', 'action_masks'], softmax)
    onnx.checker.check_model(extracted)
    target = OUT / f'stage-{step}.onnx'
    onnx.save(extracted, target)
    evaluator = ReferenceEvaluator(extracted)
    rng = np.random.RandomState(1710)
    for case in range(3):
        obs = np.zeros((1,676),np.float32) if case == 0 else rng.uniform(-1,1,(1,676)).astype(np.float32)
        mask = np.ones((1,8),np.float32)
        if case == 2: mask[0,[1,6]]=0
        expected = evaluator.run(None, {'obs_0':obs,'action_masks':mask})[0].reshape(-1)
        assert len(expected)==8 and np.isfinite(expected).all()
        fixtures.append(dict(step=step,observation=obs[0].tolist(),mask=mask[0].tolist(),probabilities=expected.tolist()))
    rows.append(dict(step=step,source=str(source.relative_to(ROOT)),sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                     asset=str(target.relative_to(ROOT)),assetSha256=hashlib.sha256(target.read_bytes()).hexdigest(),
                     purpose='Unchanged weights; expose the same masked Softmax used by the existing evaluator. No training.'))
(OUT/'catalog.json').write_text(json.dumps(dict(models=rows),ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'inference-fixtures.json').write_text(json.dumps(dict(cases=fixtures)),encoding='utf-8')
print(json.dumps(rows,indent=2))
