import copy
from pathlib import Path
from types import SimpleNamespace
import unittest
from unittest.mock import patch
import torch
import mlagents.trainers.trainer
from mlagents.trainers.ghost.trainer import GhostTrainer
from five_v10_migration import OLD,NEW,validate_pool,migrate_pool,same_state

class MigrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        root=Path(__file__).resolve().parent.parent
        cls.original=torch.load(root/'results/Manager5v5/MNG5_V5-20261016-r001/MNG5_Manager_v5/five-pool.pt',map_location='cpu')
        cls.migrated=migrate_pool(cls.original)

    def test_all_snapshot_bindings_and_retained_states(self):
        validate_pool(self.migrated)
        self.assertEqual(len(self.migrated['ghost']['policy_snapshots']),8)
        for old,new in zip(self.original['ghost']['policy_snapshots'],self.migrated['ghost']['policy_snapshots']):
            self.assertTrue(same_state(old[OLD],new[NEW]))
        self.assertTrue(same_state(self.original['ghost']['current_policy_snapshot'][OLD],self.migrated['ghost']['current_policy_snapshot'][NEW]))
        self.assertEqual(self.original['ppo_buffer'].num_experiences,2231)
        self.assertEqual(self.migrated['ppo_buffer'].num_experiences,0)
        validate_pool(self.original,OLD,self.original['contract'])

    def test_actual_sdk_swap_reads_every_snapshot_and_current_for_both_teams(self):
        for learning_team in (0,1):
            trainer=GhostTrainer.__new__(GhostTrainer)
            trainer._learning_team=learning_team
            trainer.play_against_latest_model_ratio=.5
            trainer.policy_snapshots=self.migrated['ghost']['policy_snapshots']
            trainer.current_policy_snapshot=self.migrated['ghost']['current_policy_snapshot']
            trainer.ghost_step=self.migrated['ghost']['ghost_step']
            for choice in list(range(8))+['current']:
                loaded=[];queued=[]
                policy=SimpleNamespace(load_weights=lambda weights:loaded.append(weights))
                trainer.policies={f'{NEW}?team={1-learning_team}':policy}
                trainer._team_to_name_to_policy_queue={1-learning_team:{NEW:SimpleNamespace(put=lambda p:queued.append(p))}}
                with patch('numpy.random.uniform',return_value=1. if choice=='current' else 0.),patch('numpy.random.randint',return_value=0 if choice=='current' else choice):
                    GhostTrainer._swap_snapshots(trainer)
                expected=trainer.current_policy_snapshot if choice=='current' else trainer.policy_snapshots[choice]
                self.assertEqual(len(loaded),1);self.assertTrue(same_state(loaded[0],expected[NEW]))
                self.assertEqual(queued,[policy])

    def test_legacy_internal_keys_are_rejected_before_player_launch(self):
        for field in ('policy_snapshots','current_policy_snapshot'):
            broken=copy.deepcopy(self.migrated)
            broken['ghost'][field]=copy.deepcopy(self.original['ghost'][field])
            with self.assertRaisesRegex(AssertionError,'behavior mismatch'):validate_pool(broken)

if __name__=='__main__':unittest.main()
