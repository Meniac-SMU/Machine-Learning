"""Integrity tests for the new event accounting and online rejection path."""
import json
from pathlib import Path
import tempfile
import unittest
from five_match_metrics import event_metrics
from five_trace_guard import TraceGuard


def e(kind,actor,other=-1,id=0): return dict(kind=kind,actor=actor,other=other,id=id,time=0,reason='')


class MetricsTests(unittest.TestCase):
    def test_runtime_identity_is_not_silently_mixed(self):
        with tempfile.TemporaryDirectory() as tmp:
            row=dict(contract='MNG5-OBS676-A8-R1-M2-v2',playerRuntime='tactics-v3-expansion',
                executionFeatures=[True,True,True],
                ruleOpponentTeam=-1,boundaryCorrections=0,kind='decision',team=0,action=3,
                observation=[0]*676,redScore=0,navyScore=0,reward=0)
            (Path(tmp)/'trace-test.jsonl').write_text(json.dumps(row)+'\n')
            self.assertEqual(len(TraceGuard(tmp).scan()),1)
            self.assertEqual(TraceGuard(tmp,'tactics-v3-expansion').scan(),[])

    def test_launched_and_stable_passes_not_signals_or_cancellations(self):
        events=[e('pass-signal',1,4,1),e('pass-failed',1,4,1),e('pass-kicked',1,4,2),
                e('pass-received',1,4,2),e('pass-kicked',6,8,3),e('pass-failed',6,8,3)]
        red,navy=event_metrics(events)
        self.assertEqual((red['passAttempts'],red['passSuccesses'],red['passSuccessRate']),(1,1,1))
        self.assertEqual((navy['passAttempts'],navy['passSuccesses'],navy['passSuccessRate']),(1,0,0))
        with self.assertRaises(AssertionError): event_metrics(events+[e('pass-received',1,4,2)])

    def test_shots_goals_and_new_possession_are_separate(self):
        red,navy=event_metrics([e('shot',3),e('goal',0),e('shot',3),e('possession',5),
            e('goal',0),e('shot',8),e('goal',0)])
        self.assertEqual((red['shots'],red['shotGoals'],red['goals']),(2,1,3))
        self.assertEqual(red['shotConversionRate'],.5)
        self.assertEqual(red['goalsPerShot'],1.5)
        self.assertEqual(navy['shotConversionRate'],0)

    def test_incremental_partial_line_and_reward_corruption(self):
        with tempfile.TemporaryDirectory() as tmp:
            p=Path(tmp)/'trace-test.jsonl'; guard=TraceGuard(tmp)
            row=dict(contract='MNG5-OBS676-A8-R1-M2-v2',playerRuntime='attack-keeper-v2-reception',
                ruleOpponentTeam=-1,boundaryCorrections=0,kind='decision',team=0,action=2,
                observation=[0]*676,redScore=0,navyScore=0,reward=0)
            data=json.dumps(row).encode()
            p.write_bytes(data[:80]); self.assertEqual(guard.scan(),[]); self.assertEqual(guard.rows,0)
            with p.open('ab') as f: f.write(data[80:]+b'\n')
            self.assertEqual(guard.scan(),[]); self.assertEqual(guard.rows,1)
            row['reward']=.02
            with p.open('a') as f:f.write(json.dumps(row)+'\n')
            self.assertEqual(len(guard.scan()),1)


if __name__=='__main__': unittest.main()
