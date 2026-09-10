"""Acceptance checks must not promote stale or leaking trainer summaries."""
import copy
import unittest
from types import SimpleNamespace
from unittest.mock import patch
from inspect_soccer_training import (
    inspect, l1_gate, l2_gate, l2_find_gate, l2_score_gate, l3_gate)


class L2FindAndScorePromotionGateTests(unittest.TestCase):
    @staticmethod
    def window(prefix, values):
        return {prefix + name: {
            "points": [[i * 10000, value] for i in range(1, 6)]}
            for name, value in values.items()}

    def test_l2_find_requires_five_aligned_99_percent_summaries(self):
        metrics = self.window("Soccer/Curriculum/L2Find/", {
            "Success": .99, "Possession": .99, "Episode Seconds": 15.})
        gate = l2_find_gate(metrics)
        self.assertTrue(gate["training_gate_passed"])
        self.assertEqual(.99, gate["requires_fixed_300_success_rate"])
        metrics["Soccer/Curriculum/L2Find/Success"]["points"][-1][1] = .989
        self.assertFalse(l2_find_gate(metrics)["training_gate_passed"])

    def test_l2_find_rejects_wrong_lesson_reward(self):
        metrics = self.window("Soccer/Curriculum/L2Find/", {
            "Success": 1., "Possession": 1., "Episode Seconds": 10.})
        metrics["Soccer/Reward/CurriculumGoal"] = {"points": [[50000, .4]]}
        self.assertFalse(l2_find_gate(metrics)["training_gate_passed"])

    def test_l2_score_requires_zero_own_goals(self):
        metrics = self.window("Soccer/Curriculum/L2Score/", {
            "Success": .95, "Possession": .95, "Episode Seconds": 20., "Own Goal": 0.})
        gate = l2_score_gate(metrics)
        self.assertTrue(gate["training_gate_passed"])
        self.assertEqual(0, gate["requires_fixed_300_own_goals"])
        metrics["Soccer/Curriculum/L2Score/Own Goal"]["points"][-1][1] = .001
        self.assertFalse(l2_score_gate(metrics)["training_gate_passed"])

    def test_inspect_selects_l2_find_gate(self):
        tags = {"Environment/Cumulative Reward": .1,
                "Soccer/Curriculum/L2Find/Success": 1.,
                "Soccer/Curriculum/L2Find/Possession": 1.,
                "Soccer/Curriculum/L2Find/Episode Seconds": 10.}
        data = {key: [SimpleNamespace(step=i * 10000, value=value, wall_time=float(i))
                      for i in range(1, 6)] for key, value in tags.items()}
        with patch("inspect_soccer_training.EventAccumulator") as accumulator, \
                patch("inspect_soccer_training.host_snapshot", return_value={"current": True}):
            accumulator.return_value.Tags.return_value = {"scalars": list(data)}
            accumulator.return_value.Scalars.side_effect = lambda tag: data[tag]
            result = inspect("test-run", 5)
        self.assertEqual("L2-Find", result["gate"]["lesson"])
        self.assertTrue(result["gate"]["training_gate_passed"])


class L3PromotionGateTests(unittest.TestCase):
    def setUp(self):
        self.metrics = {"Soccer/Curriculum/L3/" + name: {
            "points": [[i * 10000, value] for i in range(1, 6)]}
            for name, value in {"Success": .5, "Possession": .85,
                                "Strict Pass Completed": .85,
                                "Stable Receiver Possession": .85,
                                "Episode Seconds": 15.}.items()}
        for name, value in {"Spawn Difficulty": 1., "Waiting Assistance": 0.,
                            "Pass Aim Gate": 0.}.items():
            self.metrics["Soccer/Curriculum/L2/" + name] = {
                "points": [[i * 10000, value] for i in range(1, 6)]}

    def test_full_l3_window_passes_and_requires_all_regressions(self):
        gate = l3_gate(self.metrics)
        self.assertTrue(gate["training_gate_passed"])
        self.assertTrue(gate["requires_l0_l1_l2_regression_before_final_promotion"])

    def test_incidental_handoff_or_unstable_receiver_cannot_promote(self):
        self.metrics["Soccer/Curriculum/L3/Strict Pass Completed"]["points"][-1][1] = .84
        self.assertFalse(l3_gate(self.metrics)["training_gate_passed"])
        self.setUp()
        self.metrics["Soccer/Curriculum/L3/Stable Receiver Possession"]["points"][-1][1] = .84
        self.assertFalse(l3_gate(self.metrics)["training_gate_passed"])

    def test_l2_progress_reward_leaks_but_l3_progress_reward_is_allowed(self):
        self.metrics["Soccer/Reward/CurriculumReceiverProgress"] = {"points": [[50000, .15]]}
        self.assertTrue(l3_gate(self.metrics)["training_gate_passed"])
        self.metrics["Soccer/Reward/CurriculumGoal"] = {"points": [[50000, .4]]}
        self.assertFalse(l3_gate(self.metrics)["training_gate_passed"])


class L2PromotionGateTests(unittest.TestCase):
    def setUp(self):
        self.metrics = {"Soccer/Curriculum/L2/" + name: {
            "points": [[i * 10000, value] for i in range(1, 6)]}
            for name, value in {"Success": .5, "Possession": .85,
                                "Episode Seconds": 15., "Spawn Difficulty": 1., "Waiting Assistance": 0.,
                                "Pass Aim Gate": 0.}.items()}

    def test_pass_aim_gate_or_missing_gate_cannot_promote(self):
        key = "Soccer/Curriculum/L2/Pass Aim Gate"
        self.metrics[key]["points"][-1][1] = 1.
        self.assertFalse(l2_gate(self.metrics)["training_gate_passed"])
        del self.metrics[key]
        self.assertFalse(l2_gate(self.metrics)["training_gate_passed"])

    def test_assisted_or_missing_assistance_window_cannot_promote(self):
        key = "Soccer/Curriculum/L2/Waiting Assistance"
        self.metrics[key]["points"][-1][1] = 1.
        self.assertFalse(l2_gate(self.metrics)["training_gate_passed"])
        del self.metrics[key]
        self.assertFalse(l2_gate(self.metrics)["training_gate_passed"])

    def test_full_task_passes_but_still_requires_frozen_and_regression(self):
        gate = l2_gate(self.metrics)
        self.assertTrue(gate["training_gate_passed"])
        self.assertTrue(gate["requires_l0_l1_regression_before_final_promotion"])

    def test_empty_or_missing_difficulty_rejects(self):
        self.assertFalse(l2_gate({})["training_gate_passed"])
        del self.metrics["Soccer/Curriculum/L2/Spawn Difficulty"]
        self.assertFalse(l2_gate(self.metrics)["training_gate_passed"])

    def test_easy_task_is_not_final_promotion(self):
        self.metrics["Soccer/Curriculum/L2/Spawn Difficulty"]["points"] = [[i * 10000, 0.] for i in range(1, 6)]
        gate = l2_gate(self.metrics)
        self.assertTrue(gate["subtask_gate_passed"])
        self.assertFalse(gate["training_gate_passed"])

    def test_stale_or_failed_window_rejects(self):
        self.metrics["Soccer/Curriculum/L2/Possession"]["points"][-1][0] = 40000
        self.assertFalse(l2_gate(self.metrics)["training_gate_passed"])
        self.setUp()
        self.metrics["Soccer/Curriculum/L2/Success"]["points"][2][1] = .49
        self.assertFalse(l2_gate(self.metrics)["training_gate_passed"])

    def test_l1_reward_leak_rejects_even_with_curriculum_prefix(self):
        self.metrics["Soccer/Reward/CurriculumGoal"] = {"points": [[50000, .4]]}
        self.assertFalse(l2_gate(self.metrics)["training_gate_passed"])

    def test_direction_reward_allowed_but_does_not_substitute_success(self):
        self.metrics["Soccer/Reward/CurriculumPassOpportunityLost"] = {"points": [[50000, -.15]]}
        self.metrics["Soccer/Reward/CurriculumPassDirection"] = {"points": [[50000, .1]]}
        self.assertTrue(l2_gate(self.metrics)["training_gate_passed"])
        self.metrics["Soccer/Curriculum/L2/Success"]["points"][-1][1] = .1
        self.assertFalse(l2_gate(self.metrics)["training_gate_passed"])

    def test_invalid_numeric_metric_rejects(self):
        for value in (float("nan"), float("inf")):
            self.metrics["Soccer/Curriculum/L2/Success"]["points"][-1][1] = value
            self.assertFalse(l2_gate(self.metrics)["training_gate_passed"])


class PromotionGateTests(unittest.TestCase):
    def setUp(self):
        self.metrics = {}
        for tag, value in {"Success": .8, "Possession": .95, "Valid Shot": .9,
                           "Episode Seconds": 10., "Phase": 1.}.items():
            self.metrics["Soccer/Curriculum/L1/" + tag] = {
                "last": value, "points": [[i * 10000, value] for i in range(1, 6)]}

    def test_aligned_success_passes(self):
        self.assertTrue(l1_gate(self.metrics)["training_gate_passed"])

    def test_one_failed_window_rejects(self):
        self.metrics["Soccer/Curriculum/L1/Success"]["points"][2][1] = .7
        self.assertFalse(l1_gate(self.metrics)["training_gate_passed"])

    def test_stale_metric_rejects(self):
        self.metrics["Soccer/Curriculum/L1/Valid Shot"]["points"][-1][0] = 40000
        self.assertFalse(l1_gate(self.metrics)["training_gate_passed"])

    def test_short_window_rejects(self):
        self.metrics["Soccer/Curriculum/L1/Success"]["points"].pop()
        self.assertFalse(l1_gate(self.metrics)["training_gate_passed"])

    def test_legacy_penalty_rejects(self):
        self.metrics["Soccer/Reward/WastefulStrongKick"] = {"points": [[50000, -.003]]}
        self.assertFalse(l1_gate(self.metrics)["training_gate_passed"])

    def test_final_phase_requires_on_target(self):
        final = copy.deepcopy(self.metrics)
        final["Soccer/Curriculum/L1/Phase"] = {"last": 2., "points": [[i * 10000, 2.] for i in range(1, 6)]}
        self.assertFalse(l1_gate(final)["training_gate_passed"])
        final["Soccer/Curriculum/L1/Shot On Target"] = {"last": .8, "points": [[i * 10000, .8] for i in range(1, 6)]}
        self.assertTrue(l1_gate(final)["training_gate_passed"])

    def test_easy_spawn_cannot_promote_to_carry(self):
        self.metrics["Soccer/Curriculum/L1/Spawn Difficulty"] = {
            "last": 0., "points": [[i * 10000, 0.] for i in range(1, 6)]}
        gate = l1_gate(self.metrics)
        self.assertTrue(gate["subtask_gate_passed"])
        self.assertFalse(gate["training_gate_passed"])

    def test_mixed_spawn_window_rejects(self):
        self.metrics["Soccer/Curriculum/L1/Spawn Difficulty"] = {
            "last": 1., "points": [[i * 10000, float(i > 1)] for i in range(1, 6)]}
        self.assertFalse(l1_gate(self.metrics)["subtask_gate_passed"])

    def test_full_spawn_promotes(self):
        self.metrics["Soccer/Curriculum/L1/Spawn Difficulty"] = {
            "last": 1., "points": [[i * 10000, 1.] for i in range(1, 6)]}
        self.assertTrue(l1_gate(self.metrics)["training_gate_passed"])

    def test_historical_inspection_does_not_include_later_metrics(self):
        tags = {"Environment/Cumulative Reward": 0.8,
                "Soccer/Curriculum/L1/Success": .9,
                "Soccer/Curriculum/L1/Possession": 1.,
                "Soccer/Curriculum/L1/Valid Shot": .95,
                "Soccer/Curriculum/L1/Episode Seconds": 5.,
                "Soccer/Curriculum/L1/Phase": 1.,
                "Soccer/Curriculum/L1/Spawn Difficulty": .5}
        data = {key: [SimpleNamespace(step=i * 10000, value=value, wall_time=float(i))
                      for i in range(1, 7)] for key, value in tags.items()}
        data["Soccer/Curriculum/L1/Success"][-1].value = .1
        data["Soccer/Reward/PassSuccess"] = [SimpleNamespace(step=60000, value=.1, wall_time=6.)]
        with patch("inspect_soccer_training.EventAccumulator") as accumulator, \
                patch("inspect_soccer_training.host_snapshot", return_value={"current": True}):
            accumulator.return_value.Tags.return_value = {"scalars": list(data)}
            accumulator.return_value.Scalars.side_effect = lambda tag: data[tag]
            historical = inspect("test-run", 5, 50000)
            current = inspect("test-run", 5)
        self.assertEqual(50000, historical["summary_step"])
        self.assertTrue(historical["gate"]["subtask_gate_passed"])
        self.assertNotIn("Soccer/Reward/PassSuccess", historical["metrics"])
        self.assertTrue(historical["host_is_current_not_historical"])
        self.assertFalse(current["gate"]["subtask_gate_passed"])


if __name__ == "__main__":
    unittest.main()
