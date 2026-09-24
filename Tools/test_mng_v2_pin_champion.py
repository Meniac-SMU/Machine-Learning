import unittest
import torch
from mng_v2_pin_champion import pin


class ChampionPinTests(unittest.TestCase):
    def test_preserves_original_pool_and_deduplicates_champion(self):
        pool = dict(step=400000, pinned=[{'MNG_ManagerV2': {'weight': torch.zeros(3)}}],
                    ghost={'last_swap': 398000}, pool_rng=('fixed', [1, 2]))
        checkpoint = {'Policy': {'weight': torch.ones(3)},
                      'global_step': {'_GlobalSteps__global_step': 400000}}
        updated, identity = pin(pool, checkpoint)
        self.assertEqual(len(pool['pinned']), 1)
        self.assertEqual(len(updated['pinned']), 2)
        self.assertEqual(updated['ghost'], pool['ghost'])
        self.assertEqual(updated['pool_rng'], pool['pool_rng'])
        repeated, again = pin(updated, checkpoint)
        self.assertEqual(len(repeated['pinned']), 2)
        self.assertEqual(identity, again)

    def test_wrong_step_rejected(self):
        with self.assertRaises(AssertionError):
            pin({'step': 300000}, {'global_step': {'_GlobalSteps__global_step': 400000}})


if __name__ == '__main__':
    unittest.main()
