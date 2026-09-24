import unittest
import torch
from prepare_mng_ms3_experiment import check_receipt,fresh_actor

class ReadinessTests(unittest.TestCase):
    def test_fresh_actor_resets_only_copied_step_preserves_weights_and_source(self):
        source={'Policy':{'w':torch.tensor([3.])},'global_step':{'_GlobalSteps__global_step':torch.tensor([200120])}}
        prepared=fresh_actor(source)
        self.assertEqual(int(prepared['global_step']['_GlobalSteps__global_step']),0)
        self.assertEqual(int(source['global_step']['_GlobalSteps__global_step']),200120)
        self.assertTrue(torch.equal(source['Policy']['w'],prepared['Policy']['w']))
        self.assertNotEqual(source['Policy']['w'].data_ptr(),prepared['Policy']['w'].data_ptr())
    def test_optimizer_in_initialization_is_rejected(self):
        with self.assertRaisesRegex(AssertionError,'Actor-only'):
            fresh_actor({'Policy':{},'global_step':{},'Optimizer':{}})
    def fixture(self):
        build=dict(runtimeSha256='verified')
        preflight=dict(runtimeSha='verified',passed=True,workers=32,simultaneousBarrierReached=True,trainingSteps=0,optimizerUpdates=0)
        receipt=dict(runtimeSha='verified',checks={k:dict(passed=True) for k in ('D1D2','D3','D4','EditMode','PlayMode','Python')})
        return receipt,build,build.copy(),preflight
    def test_ready_when_all_required_evidence_passes(self):self.assertTrue(check_receipt(*self.fixture()))
    def test_unresolved_dynamic_symmetry_remains_not_ready(self):
        args=self.fixture();args[0]['checks']['D4']['passed']=False
        self.assertFalse(check_receipt(*args))
    def test_changed_runtime_rejected(self):
        args=self.fixture();args[2]['runtimeSha256']='other'
        with self.assertRaisesRegex(AssertionError,'Runtime'):check_receipt(*args)
    def test_missing_check_rejected(self):
        args=self.fixture();del args[0]['checks']['D3']
        with self.assertRaisesRegex(AssertionError,'Incomplete'):check_receipt(*args)
    def test_worker_count_rejected(self):
        args=self.fixture();args[3]['workers']=8
        with self.assertRaisesRegex(AssertionError,'32-worker'):check_receipt(*args)
if __name__=='__main__':unittest.main()
