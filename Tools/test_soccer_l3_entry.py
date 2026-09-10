import copy
import unittest
from assess_soccer_l3_entry import assess


class L2RequiredSuccessTests(unittest.TestCase):
    def setUp(self):
        self.reports = [{"status": "complete", "failure": None, "profile": "curriculum-l2",
                         "frozen": True, "optimizer_created": False, "evaluation_protocol_version": 2,
                         "sampling_seed_applied": True, "seed": seed, "policy_sampling_seed": seed,
                         "sha256": "a" * 64, "episodes": 100, "requested_spawn_difficulty": 0.,
                         "requested_waiting_assist": 1, "requested_pass_aim_gate": 0,
                         "metrics": {"Soccer/Curriculum/L2/" + key: {"count": 100, "mean": value}
                                     for key, value in {"Success": .5, "Possession": .9,
                                                        "Episode Seconds": 12.}.items()}}
                        for seed in (17423, 29471)]

    def test_entry_is_not_full_mastery_or_l1_replacement(self):
        result = assess(self.reports)
        self.assertTrue(result["l2_success_rate_precheck_passed"])
        self.assertFalse(result["l3_development_entry_passed"])
        self.assertFalse(result["full_l2_mastery_approved"])
        self.assertFalse(result["l1_replacement_approved"])

    def test_low_average_or_bad_seed_rejects(self):
        for scores in ((.49, .50), (.80, .49)):
            reports = copy.deepcopy(self.reports)
            for report, score in zip(reports, scores):
                report["metrics"]["Soccer/Curriculum/L2/Success"]["mean"] = score
            self.assertFalse(assess(reports)["l2_success_rate_precheck_passed"])

    def test_revoked_exception_cannot_approve_29_or_49_percent(self):
        self.reports[0]["metrics"]["Soccer/Curriculum/L2/Success"]["mean"] = .36
        self.reports[1]["metrics"]["Soccer/Curriculum/L2/Success"]["mean"] = .22
        self.assertFalse(assess(self.reports)["l3_development_entry_passed"])
        result = assess(self.reports, allow_near_threshold=True)
        self.assertFalse(result["l3_development_entry_passed"])
        self.assertFalse(result["near_threshold_exception_used"])
        self.assertFalse(result["l2_success_rate_precheck_passed"])
        for report in self.reports:
            report["metrics"]["Soccer/Curriculum/L2/Success"]["mean"] = .49
        self.assertFalse(assess(self.reports, allow_near_threshold=True)["l2_success_rate_precheck_passed"])

    def test_missing_short_unseeded_or_assisted_report_rejects(self):
        for key, value in (("status", "running"), ("episodes", 99), ("sampling_seed_applied", False),
                           ("requested_pass_aim_gate", 1), ("optimizer_created", True)):
            reports = copy.deepcopy(self.reports)
            reports[0][key] = value
            self.assertFalse(assess(reports)["l2_success_rate_precheck_passed"])
        self.assertFalse(assess([])["l2_success_rate_precheck_passed"])

    def test_duplicates_and_different_models_reject(self):
        self.assertFalse(assess([self.reports[0]] * 2)["l2_success_rate_precheck_passed"])
        self.reports[0]["sha256"] = "b" * 64
        self.assertFalse(assess(self.reports)["l2_success_rate_precheck_passed"])

    def test_missing_nan_metric_and_reward_leak_reject(self):
        for value in (float("nan"), None):
            reports = copy.deepcopy(self.reports)
            reports[0]["metrics"]["Soccer/Curriculum/L2/Success"]["mean"] = value
            self.assertFalse(assess(reports)["l2_success_rate_precheck_passed"])
        self.reports[0]["metrics"]["Soccer/Reward/CurriculumGoal"] = {"sum": .1}
        self.assertFalse(assess(self.reports)["l2_success_rate_precheck_passed"])


if __name__ == "__main__":
    unittest.main()
