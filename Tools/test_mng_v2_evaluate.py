import unittest
import io
import json
from pathlib import Path
import numpy as np
from mng_v2_evaluate import FrozenPolicy, summarize, execution_sample, probability_sample, write_decision_trace, join_pass_context, summarize_pass_context

class EvaluationTests(unittest.TestCase):
    def test_pass_context_links_probability_and_rejects_stale_tick(self):
        obs=[0.]*244; obs[101]=1
        decision=dict(observation=obs,valid=[True]*6,action=1,probabilities=[1/6]*6)
        policy=dict(rawCommand=1,effectiveCommand=1,maskBits=63,observationTick=17,parentCommandId=4,
            passContext=dict(version=1,tick=17,goalPause=False,ownPossession=True,
                forwardBlocked=True,hasTarget=True,receiver=2,targetDistance=12.,laneClearance=.8))
        rows=join_pass_context([decision],[policy])
        self.assertEqual(rows[0]['targetDistance'],12.)
        self.assertEqual(summarize_pass_context(rows)['own/forward-blocked']['valid'],1)
        self.assertAlmostEqual(summarize_pass_context(rows)['own/forward-blocked']['validProbabilitySum'],1/6)
        policy['passContext']['tick']=16
        with self.assertRaisesRegex(AssertionError,'observed tick'): join_pass_context([decision],[policy])
    def test_independent_reproducible_sampling_and_mask(self):
        a, b = FrozenPolicy('uniform-valid', 12), FrozenPolicy('uniform-valid', 12)
        valid=np.array([[False,False,False,True,True,True]])
        obs=np.zeros((1,244))
        left=[a.act(obs,valid) for _ in range(50)]
        right=[b.act(obs,valid) for _ in range(50)]
        self.assertEqual(left,right)
        self.assertTrue(set(left)<=set((3,4,5)))
    def test_masked_baseline_falls_back(self):
        p=FrozenPolicy('recover',0)
        self.assertEqual(p.act(np.zeros((1,244)),np.array([[0,0,0,0,1,0]],dtype=bool)),4)
        np.testing.assert_array_equal(p.last_probabilities, [0,0,0,0,1,0])
    def test_crossed_rng_matches_cluster_by_spawn_seed(self):
        rows=[]
        for seed in (1,2):
            for stream in (0,1):
                for team in (0,1):
                    rows.append(dict(seed=seed,policyTeam=team,inferenceRngSwap=stream,score=.5,
                        goalsFor=1,goalsAgainst=1,shotStrikes=1,passStrikes=0,rawEvents=[0]*11,
                        rewardedEvents=[0]*11,conditionalCommands=[[0]*6]*4,stallActivations=1,
                        episodeStallActivations=3,episodeStallActiveSeconds=5))
        report=summarize(rows)
        self.assertEqual(report['matches'],8)
        self.assertEqual(report['paired95'],[.5,.5])
        self.assertEqual(report['episodeStallActivations'],24)
        del rows[0]['episodeStallActivations']
        del rows[0]['episodeStallActiveSeconds']
        self.assertIsNone(summarize(rows)['episodeStallActivations'])
        self.assertIsNone(summarize(rows)['episodeStallActiveSeconds'])
        with self.assertRaisesRegex(AssertionError,'Incomplete'):
            summarize(rows[:-1])
    def test_duplicate_identity_rejected(self):
        rows=[dict(seed=1,policyTeam=0,score=.5)]*2
        with self.assertRaisesRegex(AssertionError,'Duplicate'): summarize(rows)
    def test_execution_uses_preceding_command_excludes_keeper_inactive_and_pause(self):
        obs=np.zeros(244); obs[116+3]=1
        for slot in (0,1,2):
            obs[133+slot*27+6]=1
            obs[133+slot*27+13+2]=1
        samples=np.zeros((2,6),dtype=np.int64)
        skills=np.zeros((2,6,13),dtype=np.int64)
        phases=np.zeros((2,6,5),dtype=np.int64)
        execution_sample(obs,samples,skills,phases)
        self.assertEqual(samples[1,3],1)
        self.assertEqual(skills.sum(),2)
        self.assertEqual(skills[1,3,6],2)
        self.assertEqual(phases[1,3,2],2)
        obs[243]=1
        execution_sample(obs,samples,skills,phases)
        self.assertEqual(samples.sum(),1)

    def test_probability_denominators_exclude_pause_and_separate_possession(self):
        samples=np.zeros(3,dtype=np.int64)
        available=np.zeros((3,6),dtype=np.int64)
        sums=np.zeros((3,6))
        obs=np.zeros(244); valid=np.array([1,0,1,1,1,1],dtype=bool)
        probs=valid.astype(float)/5
        for state in range(3):
            obs[101:103]=0
            if state<2: obs[101+state]=1
            probability_sample(obs,valid,probs,samples,available,sums)
        obs[243]=1
        probability_sample(obs,valid,probs,samples,available,sums)
        np.testing.assert_array_equal(samples,[1,1,1])
        np.testing.assert_array_equal(available[:,1],[0,0,0])
        np.testing.assert_allclose(sums.sum(axis=1),samples)

    def test_trace_and_probability_collection_do_not_change_actions_or_inputs(self):
        plain, measured = FrozenPolicy('uniform-valid',123), FrozenPolicy('uniform-valid',123)
        samples=np.zeros(3,dtype=np.int64)
        available=np.zeros((3,6),dtype=np.int64); sums=np.zeros((3,6))
        trace=io.StringIO()
        obs=np.zeros((1,244)); valid=np.ones((1,6),dtype=bool)
        for i in range(1000):
            valid[0,1]=i%2==0; obs[0,243]=i%3==0
            before_obs=obs.copy(); before_valid=valid.copy()
            expected=plain.act(obs,valid); actual=measured.act(obs,valid)
            before_probs=measured.last_probabilities.copy()
            write_decision_trace(trace,0,obs[0],valid[0],measured.last_probabilities,actual)
            probability_sample(obs[0],valid[0],measured.last_probabilities,samples,available,sums)
            self.assertEqual(expected,actual)
            np.testing.assert_array_equal(obs,before_obs)
            np.testing.assert_array_equal(valid,before_valid)
            np.testing.assert_array_equal(measured.last_probabilities,before_probs)
        self.assertEqual(len(trace.getvalue().splitlines()),1000)
        self.assertEqual(json.loads(trace.getvalue().splitlines()[-1])['action'],actual)
        self.assertEqual(samples.sum(),666)

if __name__=='__main__': unittest.main()
