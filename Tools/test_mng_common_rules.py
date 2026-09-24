import unittest
from mng_v2_evaluate import validate_common_rules


class CommonRuleEvaluationTests(unittest.TestCase):
    def test_mirrored_assignment_and_disabled_side_integrity(self):
        for team in (0, 1):
            result = {'commonRulesEnabled': [team == 0, team == 1]}
            for key in ('commonPassAttempts', 'commonClearanceAttempts', 'commonNoTargetAttempts', 'commonRuleStrikes'):
                result[key] = [int(team == 0), int(team == 1)]
            validate_common_rules(result, team, True, False)
            result['commonRuleStrikes'][1-team] = 1
            with self.assertRaisesRegex(AssertionError, 'Legacy side'):
                validate_common_rules(result, team, True, False)

    def test_wrong_side_is_rejected(self):
        with self.assertRaisesRegex(AssertionError, 'Rule assignment'):
            validate_common_rules({'commonRulesEnabled': [False, True]}, 0, True, False)
