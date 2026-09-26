import os
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import torch
from mng_v2_learn import snapshot_sha, validate_v3_pool
from mng_v3_policy_guard import validate_initial, validate_evaluation_policy, sha

class V3LineageTests(unittest.TestCase):
    def test_quarantined_candidate_is_rejected_before_manifest_loading(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            run = 'MNG_MS3V3-20260925-r001'
            candidate = root/'results'/run/'candidate.onnx'
            candidate.parent.mkdir(parents=True)
            candidate.write_bytes(b'failed candidate')
            evidence = root/'Logs/MNG-Rebuild'/run
            evidence.mkdir(parents=True)
            (evidence/'quarantine.json').write_text('{}')
            record = dict(baseOnnx={'sha256':'approved'}, excluded=[], allowedRunPattern=r'^MNG_MS3V3-\d{8}-r\d{3}$')
            with patch('mng_v3_policy_guard.ROOT', root), patch('mng_v3_policy_guard.registry', return_value=record):
                with self.assertRaisesRegex(AssertionError, 'Quarantined'):
                    validate_evaluation_policy(str(candidate))

    def setUp(self):
        self.snapshot = {'MNG_ManagerV2': {'w': torch.tensor([1.])}}
        self.identity = snapshot_sha(self.snapshot)
        self.env = patch.dict(os.environ, {'MNG_EXPERIMENT_GENERATION':'v3', 'MNG_V3_INITIAL_SNAPSHOT_SHA':self.identity, 'MNG_V2_PINNED_POLICY':''})
        self.env.start(); self.addCleanup(self.env.stop)
    def saved(self):
        return dict(generation='v3', baseSnapshotSha=self.identity, pinned=[self.snapshot])
    def test_initial_and_matching_resume(self):
        validate_v3_pool(initial=self.snapshot)
        validate_v3_pool(saved=self.saved())
    def test_old_pool_rejected(self):
        saved=self.saved(); saved.pop('generation')
        with self.assertRaises(AssertionError):validate_v3_pool(saved=saved)
    def test_extra_pinned_history_rejected(self):
        saved=self.saved();saved['pinned'].append(self.snapshot)
        with self.assertRaises(AssertionError):validate_v3_pool(saved=saved)
    def test_external_history_rejected(self):
        with patch.dict(os.environ, {'MNG_V2_PINNED_POLICY':'archived.pt'}):
            with self.assertRaises(AssertionError):validate_v3_pool()
    def test_wrong_initial_rejected(self):
        with self.assertRaises(AssertionError):validate_v3_pool(initial={'MNG_ManagerV2':{'w':torch.tensor([2.])}})
    def test_actor_and_evaluation_allowlist(self):
        with tempfile.TemporaryDirectory() as folder:
            good=Path(folder)/'good';good.write_bytes(b'approved')
            old=Path(folder)/'old';old.write_bytes(b'excluded')
            record=dict(baseActor={'sha256':sha(good)},baseOnnx={'sha256':sha(good)},allowedRunPattern=r'^MNG_MS3V3-\d{8}-r\d{3}$',excluded=[dict(onnxSha=sha(old),ptSha=sha(old))])
            with patch('mng_v3_policy_guard.registry',return_value=record):
                validate_initial(good,run_id='MNG_MS3V3-20260923-r001')
                validate_evaluation_policy(str(good))
                for source in ('R0-Full-v2','uniform-valid','recover','balanced','carry-shot'):validate_evaluation_policy(source)
                with self.assertRaises(AssertionError):validate_initial(old)
                with self.assertRaises(AssertionError):validate_initial(good,historical=old)
                with self.assertRaises(AssertionError):validate_initial(good,run_id='MNG_MS3V2-20260923-r001')
                with self.assertRaises(AssertionError):validate_evaluation_policy(str(old))

if __name__=='__main__':unittest.main()
