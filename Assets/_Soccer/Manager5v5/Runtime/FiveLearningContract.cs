using System;
using System.Collections.Generic;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    /// <summary>P8 ABI. This whitelist never reads the opponent's command, targets or reservations.</summary>
    public static class FiveLearningContract
    {
        public const string Version = "MNG5-OBS676-A8-R1-M2-v6-ALLMAX3-T7MAX2-COOLDOWN2-GK6";
        public const string BehaviorName = "MNG5_Manager_v6";
        public const int FrameSize = 168;
        public const int FrameCount = 4;
        public const float HistoryInterval = .25f;
        public const float MaximumSampleError = .0201f;
        public const int ObservationSize = FrameSize * FrameCount + FrameCount;
        public const int ActionCount = 8;
        public const string Lineage = "MNG5-v6-all-three-t7-two-consecutive-keeper-deadline";

        public static void CheckTeam(Team team)
        {
            if (team != Team.Red && team != Team.Navy) throw new ArgumentOutOfRangeException(nameof(team));
        }

        public static bool CanDecide(FiveMatch match, Team team)
        {
            CheckTeam(team);
            return !match.Paused && match.State == FiveMatchState.Playing
                && !match.DefensiveFormationActive(team)
                && match.Elapsed + .0001f >= match.Decision(team).nextDecisionAt;
        }

        public static bool Apply(FiveMatch match, Team team, int action)
        {
            CheckTeam(team);
            if (action < 0 || action >= ActionCount) throw new ArgumentOutOfRangeException(nameof(action));
            if (!CanDecide(match, team)) return false;
            if (match.ruleManagers[(int)team]) throw new InvalidOperationException("Two manager authorities on one team.");
            return match.SelectCommand(team, (FiveCommand)action, Version);
        }

        public static float[] CaptureFrame(FiveMatch match, Team team)
        {
            CheckTeam(team);
            var values = new List<float>(FrameSize);
            var sign = FiveMatch.AttackSign(team);
            var length = match.geometry.HalfLength;
            var width = match.geometry.HalfWidth;
            var own = (int)team * 5;
            var enemy = (int)FiveMatch.Other(team) * 5;
            void Add(float value)
            {
                if (!float.IsFinite(value)) throw new InvalidOperationException("Non-finite learning observation.");
                values.Add(Mathf.Clamp(value, -1, 1));
            }
            void OneHot(int index, int count) { for (var i = 0; i < count; i++) Add(i == index ? 1 : 0); }
            void Position(Vector2 p) { Add(p.x * sign / length); Add(p.y * sign / width); }
            void Velocity(Vector2 v, float maximum) { Add(v.x * sign / maximum); Add(v.y * sign / maximum); }
            Position(match.BallPosition);
            Velocity(match.BallVelocity, match.profile.maximumBallSpeed);
            var owner = match.Owner < 0 ? 0 : match.players[match.Owner].team == team ? 1 + match.Owner % 5 : 6 + match.Owner % 5;
            OneHot(owner, 11);
            var ownScore = team == Team.Red ? match.RedScore : match.NavyScore;
            var enemyScore = team == Team.Red ? match.NavyScore : match.RedScore;
            Add(ownScore / (1f + ownScore)); Add(enemyScore / (1f + enemyScore));
            Add(Mathf.Max(0, match.profile.duration - match.Elapsed) / match.profile.duration);
            OneHot((int)match.State, 3);
            Add(match.Paused ? 1 : 0);
            var decision = match.Decision(team);
            OneHot((int)decision.command, 8);
            Add(Mathf.Max(0, decision.nextDecisionAt - match.Elapsed) / match.profile.decisionSeconds);
            var age = Mathf.Max(0, match.Elapsed - decision.selectedAt);
            Add(age / (1 + age));
            Add(match.ObservedStallSeconds / match.profile.duration);
            Add(match.ObservedUnproductiveSeconds(team) / match.profile.duration);
            var ownPass = match.PassPhase != FivePassPhase.None && match.PassSender >= 0 && match.players[match.PassSender].team == team;
            OneHot(ownPass ? (int)match.PassPhase : 0, 3);
            OneHot(ownPass ? match.PassSender % 5 : -1, 5);
            OneHot(ownPass ? match.PassReceiver % 5 : -1, 5);
            foreach (var offset in new[] {own, enemy})
                for (var slot = 0; slot < 5; slot++)
                {
                    var player = match.players[offset + slot];
                    Position(player.Position); Velocity(player.Velocity, match.profile.maxSpeed);
                    Add(player.Forward.x * sign); Add(player.Forward.y * sign);
                    Add(player.IsKeeper ? 1 : player.Stamina.Value);
                    OneHot((int)player.role, 5);
                }
            Add(match.profile.goalPause > 0 ? Mathf.Max(0, match.PauseRemaining) / match.profile.goalPause : 0);
            if (values.Count != FrameSize) throw new InvalidOperationException("Observation frame ABI drift.");
            return values.ToArray();
        }

        public static string[] FeatureNames()
        {
            var names = new List<string>(ObservationSize);
            for (var frame = 0; frame < FrameCount; frame++)
                foreach (var name in FrameFeatureNames()) names.Add($"history.{frame}.{name}");
            for (var frame = 0; frame < FrameCount; frame++) names.Add($"history.{frame}.valid");
            return names.ToArray();
        }

        public static string[] FrameFeatureNames()
        {
            var names = new List<string> {"ball.x", "ball.z", "ball.vx", "ball.vz", "owner.none"};
            foreach (var side in new[] {"own", "opponent"}) for (var i = 0; i < 5; i++) names.Add($"owner.{side}.{i}");
            names.AddRange(new[] {"score.own", "score.opponent", "time.remaining", "state.playing", "state.goalPause", "state.finished", "paused"});
            for (var i = 0; i < 8; i++) names.Add($"command.own.T{i + 1}");
            names.AddRange(new[] {"command.wait", "command.age", "observed.ballStall", "observed.ownNoProgress", "pass.own.none", "pass.own.preparing", "pass.own.inFlight"});
            foreach (var role in new[] {"sender", "receiver"}) for (var i = 0; i < 5; i++) names.Add($"pass.own.{role}.{i}");
            foreach (var side in new[] {"own", "opponent"})
                for (var slot = 0; slot < 5; slot++)
                    foreach (var field in new[] {"x", "z", "vx", "vz", "forwardX", "forwardZ", "stamina", "role.keeper", "role.defenderLeft", "role.defenderRight", "role.attackerLeft", "role.attackerRight"})
                        names.Add($"player.{side}.{slot}.{field}");
            names.Add("time.goalPause");
            if (names.Count != FrameSize) throw new InvalidOperationException("Feature name ABI drift.");
            return names.ToArray();
        }
    }

    /// <summary>Consume score deltas once. A goal restart must not reset this ledger.</summary>
    public sealed class FiveScoreReward
    {
        int red, navy;
        public float RedTotal { get; private set; }
        public void Begin(FiveMatch match) { red = match.RedScore; navy = match.NavyScore; RedTotal = 0; }
        public float Consume(FiveMatch match)
        {
            if (match.RedScore < red || match.NavyScore < navy) throw new InvalidOperationException("Reset requires a new reward episode.");
            var delta = (match.RedScore - red) - (match.NavyScore - navy);
            red = match.RedScore; navy = match.NavyScore; RedTotal += delta;
            return delta;
        }
    }
}
