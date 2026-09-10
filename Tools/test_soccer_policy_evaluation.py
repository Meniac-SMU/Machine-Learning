"""Reject incorrect lesson/profile pairs before a Player or output is created."""
import contextlib
import io
import unittest
from evaluate_soccer_policy import (
    build_parser, validate_arguments, l2_advantageous_target_count,
    l2_preparation_proxy, summarize_evaluation_metrics)


class EvaluationArgumentTests(unittest.TestCase):
    def test_batched_episode_metrics_trim_to_exact_requested_count(self):
        success_key = "Soccer/Curriculum/L2Find/Success"
        metrics = {
            success_key: [1.] * 284 + [0.] * 17,
            "Soccer/Curriculum/L2Find/Episode Seconds": [5.] * 301,
            "Soccer/Reward/CurriculumFindProgress": [.01] * 500,
            "Soccer/Curriculum/L2Find/Diagnostic Lateral Wide Success": [1.] * 120,
        }
        episodes, raw, rendered = summarize_evaluation_metrics(metrics, success_key, 300)
        self.assertEqual(300, episodes)
        self.assertEqual(301, raw)
        self.assertEqual(300, rendered[success_key]["count"])
        self.assertEqual(284., rendered[success_key]["sum"])
        self.assertEqual(300, rendered["Soccer/Curriculum/L2Find/Episode Seconds"]["count"])
        self.assertEqual(500, rendered["Soccer/Reward/CurriculumFindProgress"]["count"])
        self.assertEqual(120, rendered["Soccer/Curriculum/L2Find/Diagnostic Lateral Wide Success"]["count"])

    def test_preparation_proxy_is_read_only_and_excludes_waiting_or_unready(self):
        vector = [0.] * 43
        vector[7], vector[12], vector[14], vector[15] = 1.5 / 80., 1., 1., 8. / 80.
        action = [1, 0, 2, 0]
        masks = [[False] * 3 for _ in range(4)]
        masks[3] = [False, True, True]
        original = vector[:]
        proxy = l2_preparation_proxy(vector, action, masks)
        self.assertEqual(proxy["Target Present"], 1.)
        self.assertEqual(proxy["Kick Masked"], 1.)
        self.assertEqual(proxy["Forward Action"], 1.)
        self.assertEqual(proxy["Turn Action"], 1.)
        self.assertEqual(vector, original)
        self.assertIsNone(l2_preparation_proxy(vector, action, [[False, True, True]] * 4))
        vector[14] = 0.
        self.assertIsNone(l2_preparation_proxy(vector, action, masks))

    def test_preparation_proxy_rejects_far_nonpossessed_and_invalid_vectors(self):
        for ball_distance, possession in [(2., 1.), (1., 0.)]:
            vector = [0.] * 43
            vector[7], vector[12], vector[14] = ball_distance / 80., possession, 1.
            self.assertIsNone(l2_preparation_proxy(vector, [0] * 4))
        with self.assertRaises(ValueError):
            l2_preparation_proxy([0.] * 42, [0] * 4)
        self.assertIsNone(l2_preparation_proxy([float('nan')] * 43, [0] * 4))

    def test_l2_diagnostic_finds_forward_but_not_crowded_or_backward_target(self):
        vector = [0.] * 43
        vector[15] = 8. / 80.
        vector[19] = -8. / 80.
        vector[23] = 2. / 80.
        self.assertEqual(l2_advantageous_target_count(vector), 1)

    def test_l2_diagnostic_rejects_wrong_tensor(self):
        with self.assertRaises(ValueError):
            l2_advantageous_target_count([0.] * 42)

    def parse(self, *extra):
        return build_parser().parse_args(["--checkpoint", "unused.pt", "--output", "unused.json", *extra])

    def test_known_lessons_match_profile(self):
        for lesson in ("L0", "L1", "L2", "L3"):
            validate_arguments(self.parse("--lesson", lesson, "--profile", "curriculum-" + lesson.lower()))
        validate_arguments(self.parse("--lesson", "L2Find", "--profile", "curriculum-l2-find"))
        validate_arguments(self.parse("--lesson", "L2Score", "--profile", "curriculum-l2-score"))

    def test_pass_gate_is_explicit_and_defaults_off(self):
        self.assertEqual(self.parse().pass_aim_gate, 0)
        self.assertEqual(self.parse().pass_advice, 0)
        self.assertEqual(self.parse("--pass-advice", "1").pass_advice, 1)
        self.assertEqual(self.parse("--pass-aim-gate", "1").pass_aim_gate, 1)

    def test_numeric_lesson_typo_rejected(self):
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as caught:
            self.parse("--lesson", "1")
        self.assertEqual(caught.exception.code, 2)

    def test_mismatched_profile_rejected(self):
        with self.assertRaisesRegex(ValueError, "requires --profile curriculum-l0"):
            validate_arguments(self.parse("--lesson", "L0"))

    def test_nonpositive_limits_rejected(self):
        for option in ("--episodes", "--max-seconds"):
            with self.assertRaisesRegex(ValueError, "must be positive"):
                validate_arguments(self.parse(option, "0"))

    def test_invalid_seed_rejected_before_player_start(self):
        for seed in ("-1", "2147483648"):
            with self.assertRaisesRegex(ValueError, "Evaluation seed"):
                validate_arguments(self.parse("--seed", seed))


if __name__ == "__main__":
    unittest.main()
