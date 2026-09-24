import copy
import unittest
from mng_v2_assess import preparation, promotion

def fixture(kind,rate):
    return dict(manifest=dict(candidate=kind,opponent='R0-Full-v2',runtimeSha='same',schema='244',
        seconds=300,protocol='frozen',inference='sample',seedOffset=1),
        summary=dict(matches=40,scoreRate=rate,integrity=True),
        matches=[dict(policyTeam=i%2,shotStrikes=1,observedRecoveries=1) for i in range(40)])

class GateTests(unittest.TestCase):
    def promotion_reports(self):
        holdout=fixture('model',.7)
        holdout['manifest']['opponent']='step0'
        holdout['summary'].update(matches=80,paired95=[.6,.8],perTeam=[.7,.7],goalsFor=100,goalsAgainst=60)
        holdout['matches']=[dict(seed=100+i//2,policyTeam=i%2) for i in range(80)]
        history=copy.deepcopy(holdout)
        history['summary']['matches']=40
        history['matches']=[dict(seed=1+i//2,policyTeam=i%2) for i in range(40)]
        return holdout,[history],400000,fixture('model',.8),fixture('uniform-valid',.2)
    def test_promotion_rejects_borderline_confidence_and_history_weakness(self):
        args=self.promotion_reports()
        self.assertTrue(promotion(*args)['passed'])
        args[0]['summary']['paired95'][0]=.5
        self.assertFalse(promotion(*args)['passed'])
        args[0]['summary']['paired95'][0]=.6
        args[1][0]['summary']['scoreRate']=.44
        self.assertFalse(promotion(*args)['passed'])
    def test_holdout_seed_reuse_rejected(self):
        args=self.promotion_reports()
        args[0]['matches'][0]['seed']=1
        with self.assertRaisesRegex(AssertionError,'Holdout seeds reused'): promotion(*args)
    def test_exact_ten_percentage_points_passes(self):
        self.assertTrue(preparation(fixture('model',.5),fixture('uniform-valid',.4))['passed'])
    def test_missing_recovery_prevents_self_play(self):
        report=fixture('model',.8)
        for row in report['matches']:
            if row['policyTeam']==1: row['observedRecoveries']=0
        self.assertFalse(preparation(report,fixture('uniform-valid',.2))['passed'])
    def test_mixed_runtime_rejected(self):
        baseline=fixture('uniform-valid',.2); baseline['manifest']['runtimeSha']='different'
        with self.assertRaisesRegex(AssertionError,'runtimeSha'): preparation(fixture('model',.8),baseline)

if __name__=='__main__': unittest.main()
