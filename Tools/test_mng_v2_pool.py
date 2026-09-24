import copy
from collections import deque
from pathlib import Path
from queue import Queue
from tempfile import TemporaryDirectory
from types import SimpleNamespace
from unittest import TestCase, main
from unittest.mock import patch

import numpy as np
import torch
from mng_v2_learn import MNGV2GhostTrainer, GhostTrainer, create_name_behavior_id, snapshot_sha


class Policy:
    def __init__(self, weights): self.weights = copy.deepcopy(weights)
    def get_weights(self): return copy.deepcopy(self.weights)
    def load_weights(self, weights): self.weights = copy.deepcopy(weights)


def fixture(directory):
    obj = MNGV2GhostTrainer.__new__(MNGV2GhostTrainer)
    obj.trainer = SimpleNamespace(get_step=4096)
    obj.brain_name = "MNG_ManagerV2"
    obj.sidecar = Path(directory) / "pool.pt"
    obj.audit = Path(directory) / "audit.jsonl"
    obj.rng = np.random.RandomState(19)
    obj.current_policy_snapshot = {obj.brain_name: {"weight": torch.tensor([3.0])}}
    obj.policy_snapshots = [{obj.brain_name: {"weight": torch.tensor([2.0])}}]
    obj.pinned = [{obj.brain_name: {"weight": torch.tensor([1.0])}}]
    obj.ghost_step = 3210
    obj.snapshot_counter = 1
    obj._learning_team = 1
    obj.wrapped_trainer_team = 0
    obj.last_save, obj.last_swap, obj.last_team_change = 4000, 3000, 2048
    obj.policy_elos = [1200.0] * 31
    obj.current_opponent = -1
    obj.controller = SimpleNamespace(_queue=deque([0]), _learning_team=1, _changed_training_team=False)
    obj.policies = {create_name_behavior_id(obj.brain_name, t): Policy(obj.current_policy_snapshot[obj.brain_name]) for t in range(2)}
    obj._team_to_name_to_policy_queue = {t: {obj.brain_name: Queue()} for t in range(2)}
    obj.pending_restore = None
    return obj


class PoolTests(TestCase):
    def test_new_pool_pins_initial_and_historical_actor_without_optimizer(self):
        with TemporaryDirectory() as directory:
            obj=fixture(directory);obj.pinned=[]
            path=Path(directory)/'historical.pt'
            torch.save({'Policy':{'weight':torch.tensor([9.0])},'Optimizer:unused':{'ignored':True}},path)
            with patch.object(GhostTrainer,'_save_snapshot'), patch.dict('os.environ',{'MNG_V2_PINNED_POLICY':str(path)}):
                obj._save_snapshot()
            self.assertEqual(len(obj.pinned),2)
            self.assertEqual(float(obj.pinned[1]['MNG_ManagerV2']['weight'][0]),9.0)
            self.assertEqual(set(obj.pinned[1]),{'MNG_ManagerV2'})
            self.assertEqual(float(obj.current_policy_snapshot['MNG_ManagerV2']['weight'][0]),3.0)

    def test_exposure_attributes_consumed_steps_to_team_before_switch(self):
        with TemporaryDirectory() as directory:
            obj=fixture(directory)
            def advance(trainer):
                trainer.trainer.get_step+=128
                trainer.ghost_step+=256
                trainer._learning_team=0
            with patch.object(GhostTrainer,'advance',advance), patch.object(obj,'_audit') as audit:
                obj.advance()
            audit.assert_called_once_with('learning-exposure',start_step=4096,end_step=4224,
                learning_team=1,next_learning_team=0,learning_transitions=128,ghost_transitions=256)

    def test_resume_preserves_pool_rng_counters_and_next_opponents(self):
        with TemporaryDirectory() as directory:
            first = fixture(directory)
            for _ in range(13): first._swap_snapshots()
            with patch.object(GhostTrainer, "save_model"):
                first.save_model()
            resumed = fixture(directory)
            resumed.pending_restore = torch.load(first.sidecar, weights_only=False)
            resumed.last_save = 0
            resumed._learning_team = 0
            with patch.object(GhostTrainer, "advance"):
                resumed.advance()
            self.assertEqual((resumed.last_save, resumed.last_swap, resumed.last_team_change), (4000, 3000, 2048))
            self.assertEqual(resumed._learning_team, 1)
            self.assertEqual(list(resumed.controller._queue), [0])
            self.assertEqual(snapshot_sha(first.pinned[0]), snapshot_sha(resumed.pinned[0]))
            for _ in range(50):
                first._swap_snapshots(); resumed._swap_snapshots()
                a = first.policies[create_name_behavior_id(first.brain_name, 0)].weights
                b = resumed.policies[create_name_behavior_id(first.brain_name, 0)].weights
                self.assertTrue(torch.equal(a["weight"], b["weight"]))

    def test_optimizer_step_mismatch_fails_closed(self):
        with TemporaryDirectory() as directory:
            obj = fixture(directory)
            obj.pending_restore = {"step": 1}
            with self.assertRaisesRegex(RuntimeError, "checkpoint mismatch"):
                obj.advance()

    def test_pool_weights_are_not_aliased(self):
        with TemporaryDirectory() as directory:
            obj = fixture(directory)
            original = snapshot_sha(obj.pinned[0])
            obj.current_policy_snapshot[obj.brain_name]["weight"].add_(1)
            self.assertEqual(snapshot_sha(obj.pinned[0]), original)


if __name__ == "__main__": main()
