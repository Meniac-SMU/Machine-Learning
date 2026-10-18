using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    // Optional presentation input. Unset in training/evaluation, with no change to their execution.
    public sealed partial class FiveMatch
    {
        public int HumanPlayerIndex { get; private set; } = -1;
        float humanThrottle, humanTurn, humanKickUntil;
        int humanKick;
        public event Action<FiveEvent> EventRecorded;
        public event Action<FivePlayer> BallContact;
        public bool IsHuman(FivePlayer player) => player != null && player.Index == HumanPlayerIndex;

        public void SetHumanControl(bool active)
        {
            HumanPlayerIndex = active ? 3 : -1; // RED / AttackerLeft = attacker 1.
            ClearHumanInput();
            if (shotPlayer == 3) shotPlayer = -1;
            if (PassPhase == FivePassPhase.Preparing && PassSender == 3) CancelPass("human-control-transfer");
            players[3].Evading = false;
            players[3].SetTarget(players[3].Position, players[3].Position + players[3].Forward);
            Record("human-control", 3, reason: active ? "human" : "manager");
        }
        public void ClearHumanInput() { humanThrottle = humanTurn = 0; humanKick = 0; humanKickUntil = 0; }
        public void SetHumanInput(float throttle, float turn, bool pass, bool shoot)
        {
            if (HumanPlayerIndex < 0 || Paused || State != FiveMatchState.Playing) { ClearHumanInput(); return; }
            humanThrottle = Mathf.Clamp(throttle, -1, 1); humanTurn = Mathf.Clamp(turn, -1, 1);
            if (pass || shoot) { humanKick = shoot ? 2 : 1; humanKickUntil = Elapsed + .25f; }
        }
        void ApplyHumanControl(float dt)
        {
            if (HumanPlayerIndex < 0) return;
            var player = players[HumanPlayerIndex];
            player.Evading = false;
            player.SetManualDrive(humanThrottle, humanTurn);
            if (humanKick == 0) return;
            if (Elapsed > humanKickUntil) { humanKick = 0; return; }
            // Same real front pocket, reach and cooldown as the other players; no remote kick.
            if (Elapsed < player.KickReadyAt || !InDribbleArea(player) || !ClearReach(player)) return;
            if (Owner >= 0 && Owner != player.Index) return;
            var direction = player.Forward;
            var shoot = humanKick == 2; humanKick = 0;
            shotPlayer = -1;
            if (PassPhase != FivePassPhase.None) CancelPass("human-kick");
            pendingReception = -1;
            if (shoot)
            {
                Launch(direction, profile.maximumBallSpeed, player);
                Shots++; Record("shot", player.Index, reason: "human");
                return;
            }
            FivePlayer receiver = null; float best = float.PositiveInfinity;
            foreach (var candidate in players)
            {
                if (candidate == player || candidate.team != player.team) continue;
                var offset = candidate.Position - BallPosition;
                if (Vector2.Dot(offset.normalized, direction) < .7f) continue;
                var cost = Vector2.Angle(direction, offset) * 2 + offset.magnitude;
                if (cost < best) { best = cost; receiver = candidate; }
            }
            var distance = receiver == null ? 30 : Vector2.Distance(BallPosition, receiver.Position);
            var speed = Mathf.Clamp(distance / 1.7f + profile.EffectiveFreeBallDamping * distance, 12, profile.maximumBallSpeed);
            Launch(direction, speed, player);
            PassId++; PassAttempts++;
            PassSender = player.Index; PassReceiver = receiver == null ? -1 : receiver.Index;
            // An aimed pass may fail. Only a real stable teammate reception counts as success.
            if (receiver != null)
            {
                receiverHold = receiver.Position; PassPhase = FivePassPhase.InFlight;
                var drag = profile.EffectiveFreeBallDamping;
                var travel = drag > .001f ? -Mathf.Log(Mathf.Max(.01f, 1-drag*distance/speed))/drag : distance/speed;
                passDeadline = Elapsed + Mathf.Max(6, travel + 1.5f);
            }
            Record("pass-kicked", player.Index, PassReceiver, PassId, "human");
        }
    }
}
