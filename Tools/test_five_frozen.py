"""Reject stale contracts, forbidden selections and shaped rewards in the new run."""
import json
from pathlib import Path
import tempfile
import unittest
from five_frozen_trace_guard import TraceGuard

class FrozenGuardTests(unittest.TestCase):
    def rows(self, actions):
        streak = [0, 0]; blocked = [0, 0]
        for action in actions:
            for k, command in enumerate((1, 6)):
                if action == command:
                    streak[k] += 1
                    if streak[k] == 2: blocked[k] = 2
                else:
                    streak[k] = 0; blocked[k] = max(0, blocked[k]-1)
            yield dict(contract='MNG5-OBS676-A8-R1-M2-v5-T2T7MAX2-COOLDOWN2-GK6',
                playerRuntime='tactics-v8-stamina-owned-turn', executionFeatures=[True]*3,
                ruleOpponentTeam=-1, boundaryCorrections=0, kind='decision', team=0,
                action=action, episode=1, observation=[0]*676, redScore=0, navyScore=0,
                reward=0, longPassBlockedSelections=blocked[0], recoverBlockedSelections=blocked[1])

    def check(self, rows):
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp)/'trace-test.jsonl').write_text(''.join(json.dumps(r)+'\n' for r in rows))
            return TraceGuard(tmp).scan()

    def test_independent_cooldowns(self):
        self.assertEqual(self.check(self.rows([1,1,6,6,1,1,6,6])), [])

    def test_third_consecutive_rejected(self):
        for action in (1,6):
            self.assertTrue(self.check(self.rows([action]*3)))

    def test_second_waiting_choice_required(self):
        self.assertTrue(self.check(self.rows([6,6,0,6])))

    def test_stale_contract_and_reward_and_keeper_decision_rejected(self):
        for field, value in [('contract','MNG5-OBS676-A8-R1-M2-v2'),('reward',.01)]:
            rows=list(self.rows([0])); rows[0][field]=value
            self.assertTrue(self.check(rows))
        rows=list(self.rows([0])); rows[0]['observation'][10]=1
        self.assertTrue(self.check(rows))

if __name__ == '__main__': unittest.main()
