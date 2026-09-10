"""L2 50% evidence precheck. Provisional L3 entry was revoked by the user."""
import argparse
import json
import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ALLOWED_REWARDS = {
    "CurriculumPossessionEstablished", "CurriculumSupportShape", "CurriculumLessonSuccess",
    "CurriculumPassAttempt", "CurriculumPassReception", "CurriculumPassDelivered", "CurriculumPassDirection",
}


def assess(reports, allow_near_threshold=False):
    # Kept for callers of the revoked policy; this argument grants no exception.
    rows = []
    for report in reports:
        metrics = report.get("metrics", {})
        row = {"seed": report.get("seed"), "episodes": report.get("episodes", 0),
               "sha256": report.get("sha256"), "difficulty": report.get("requested_spawn_difficulty"),
               "waiting_assist": report.get("requested_waiting_assist")}
        valid = (report.get("status") == "complete" and report.get("failure") is None
                 and report.get("profile") == "curriculum-l2" and report.get("frozen") is True
                 and report.get("optimizer_created") is False
                 and report.get("evaluation_protocol_version") == 2
                 and report.get("sampling_seed_applied") is True
                 and report.get("policy_sampling_seed") == row["seed"]
                 and isinstance(row["seed"], int) and isinstance(row["episodes"], int)
                 and row["episodes"] >= 100 and row["difficulty"] in (0., .5, 1.)
                 and row["waiting_assist"] in (0, 1)
                 and report.get("requested_pass_aim_gate") == 0
                 and bool(re.fullmatch(r"[0-9a-fA-F]{64}", row["sha256"] or "")))
        for name in ("Success", "Possession", "Episode Seconds"):
            sample = metrics.get("Soccer/Curriculum/L2/" + name, {})
            value = sample.get("mean")
            numeric = isinstance(value, (int, float)) and math.isfinite(value)
            valid = valid and numeric and sample.get("count") == row["episodes"]
            valid = valid and (value >= 0 if numeric else False)
            if name != "Episode Seconds":
                valid = valid and (value <= 1 if numeric else False)
            row[name] = value if numeric else None
        leaks = [tag for tag, sample in metrics.items() if tag.startswith("Soccer/Reward/")
                 and tag.rsplit("/", 1)[-1] not in ALLOWED_REWARDS
                 and (not isinstance(sample.get("sum"), (int, float))
                      or not math.isfinite(sample["sum"]) or abs(sample["sum"]) > 1e-8)]
        row["valid"] = bool(valid and not leaks)
        row["disallowed_rewards"] = leaks
        rows.append(row)
    comparable = (len(rows) >= 2 and all(row["valid"] for row in rows)
                  and len({row["seed"] for row in rows}) == len(rows)
                  and len({row["sha256"] for row in rows}) == 1
                  and len({(row["difficulty"], row["waiting_assist"]) for row in rows}) == 1)
    total = sum(row["episodes"] for row in rows) if comparable else 0
    success = sum(row["Success"] * row["episodes"] for row in rows) / total if total else None
    supporting_checks = bool(comparable and all(
        row["Success"] >= .50 and row["Possession"] >= .85 and row["Episode Seconds"] <= 15.
        for row in rows))
    passed = bool(supporting_checks and success >= .50)
    return {"assessment": "l2_50_percent_required_l3_entry_revoked_20260905",
            "l3_development_entry_passed": False, "full_l2_mastery_approved": False,
            "l2_success_rate_precheck_passed": passed,
            "near_threshold_exception_used": False,
            "l1_replacement_approved": False, "combined_success": success,
            "episodes": total, "comparable_reports": comparable,
            "requirements": {"combined_success": .50, "each_seed_success": .50,
                             "each_seed_possession": .85, "max_episode_seconds": 15.,
                             "distinct_seeds": 2, "episodes_per_seed": 100,
                             "explicit_near_threshold_tolerance": 0.,
                             "pass_aim_gate": 0, "waiting_assist_allowed_for_entry": False},
            "evaluations": rows,
            "remaining": ["L3 blocked until L2 approval; no near-threshold exception",
                          "Require five consecutive final unassisted training summaries and independent frozen evaluation of at least 300 episodes",
                          "Require L0/L1 regressions and preserve approved L1",
                          "No incidental contact success; explicit advantageous pass and confirmed receiver remain required"]}


def inside_project(value):
    path = (ROOT / value).resolve()
    if ROOT not in path.parents:
        raise ValueError("Evidence paths must stay inside the Soccer project")
    return path


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--reports", nargs="+", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--near-threshold-reason", help="REVOKED: accepted for compatibility but cannot relax the 50 percent requirement")
    args = parser.parse_args()
    output = inside_project(args.output)
    if output.exists():
        raise FileExistsError("Choose a new output; original assessments must be preserved")
    report = assess([json.loads(inside_project(path).read_text(encoding="utf-8")) for path in args.reports],
                    allow_near_threshold=bool(args.near_threshold_reason))
    report["near_threshold_reason"] = args.near_threshold_reason
    report["source_reports"] = args.reports
    output.parent.mkdir(parents=True, exist_ok=True)
    rendered = json.dumps(report, indent=2)
    output.write_text(rendered, encoding="utf-8")
    print(rendered)
