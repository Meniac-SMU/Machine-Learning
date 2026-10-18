import unittest
from mng_ps_compare import paired_delta


class ComparisonTests(unittest.TestCase):
    def test_paired_identity_and_known_change(self):
        parent=[dict(seed=s,policyTeam=t,score=.5) for s in range(20) for t in (0,1)]
        candidate=[dict(r,score=.75) for r in parent]
        result=paired_delta(candidate,parent)
        self.assertEqual(result['delta'],.25)
        self.assertEqual(result['pairedSeed95'],[.25,.25])
        with self.assertRaises(AssertionError):paired_delta(candidate[:-1],parent)
        with self.assertRaises(AssertionError):paired_delta(candidate+candidate[:1],parent)


if __name__=='__main__':unittest.main()
