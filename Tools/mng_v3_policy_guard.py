"""Active v3 policy lineage. Archived v2 self-play is never an input."""
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
REGISTRY = ROOT / 'docs/soccer/training/ms-v3-model-registry.json'

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def registry():
    return json.loads(REGISTRY.read_text(encoding='utf-8-sig'))

def validate_initial(actor, historical=None, run_id=None):
    record = registry()
    assert not historical, 'v3 forbids external historical policies'
    assert sha(actor) == record['baseActor']['sha256'], 'v3 requires the approved MS2 actor'
    if run_id is not None:
        assert re.fullmatch(record['allowedRunPattern'], run_id), 'Invalid MS3-v3 run name'
    return record

def validate_evaluation_policy(source):
    if source in ('R0-Full-v2', 'uniform-valid', 'recover', 'balanced', 'carry-shot'):
        return
    record = registry()
    path = Path(source).resolve()
    identity = sha(path)
    if identity == record['baseOnnx']['sha256']:
        return
    assert all(identity not in (old['onnxSha'], old['ptSha']) for old in record['excluded']), 'Archived 400k policy is excluded from v3'
    relative = path.relative_to(ROOT / 'results')
    run_id = relative.parts[0]
    assert re.fullmatch(record['allowedRunPattern'], run_id), 'Only MS2 or new MS3-v3 candidates are allowed'
    manifest = json.loads((ROOT / 'Logs/MNG-Rebuild' / run_id / 'manifest.json').read_text(encoding='utf-8-sig'))
    assert manifest['generation'] == 'v3' and not manifest['historicalPolicy'], 'Invalid v3 lineage'
    assert manifest['baseActorSourceSha'] == record['baseActor']['sha256'], 'Candidate did not start from the approved MS2 actor'
