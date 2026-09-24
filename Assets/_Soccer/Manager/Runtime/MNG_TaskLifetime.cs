using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    /// <summary>Single-player task ownership. Preparation is interruptible; only extension is protected.</summary>
    public sealed class MNG_TaskLifetime
    {
        public MNG_PlayerTask Current { get; private set; }
        public bool KickConsumed { get; private set; }
        public bool HasDeferred { get; private set; }
        public MNG_PlayerTask Deferred { get; private set; }
        long m_Revision = -1;

        public MNG_TaskResult Request(MNG_PlayerTask task, long tick, float now, bool owner, bool valid, bool committed)
        {
            if (task.Revision < m_Revision) return Result(task, MNG_TaskResultKind.RejectedStale, tick);
            if (!owner) return Result(task, MNG_TaskResultKind.RejectedOwner, tick);
            if (!valid || !MNG_MatchSnapshot.IsFinite(task.ExpirySeconds)
                || !MNG_MatchSnapshot.IsFinite(task.Target.x) || !MNG_MatchSnapshot.IsFinite(task.Target.y)
                || (int)task.Skill < 0 || (int)task.Skill > 12 || task.ReceiverSlot < -1 || task.ReceiverSlot > 3)
                return Result(task, MNG_TaskResultKind.RejectedInvalidState, tick);
            if (task.ExpirySeconds <= now) return Result(task, MNG_TaskResultKind.Expired, tick);
            m_Revision = task.Revision;
            if (committed)
            {
                // Keep the original queue deadline even when the latest command replaces its contents.
                if (HasDeferred) task.ExpirySeconds = Mathf.Min(task.ExpirySeconds, Deferred.ExpirySeconds);
                Deferred = task;
                HasDeferred = true;
                return Result(task, MNG_TaskResultKind.DeferredCommit, tick);
            }
            if (!KickConsumed && Current.ExpirySeconds > now && IsKick(task.Skill)
                && task.CommonRule == Current.CommonRule && task.PassBuildRule == Current.PassBuildRule
                && task.Skill == Current.Skill && task.ReceiverSlot == Current.ReceiverSlot
                && task.Target == Current.Target)
                return Result(Current, MNG_TaskResultKind.RetainedEquivalent, tick);
            Current = task;
            KickConsumed = false;
            return Result(task, MNG_TaskResultKind.Accepted, tick);
        }

        public bool TakeDeferred(out MNG_PlayerTask task)
        {
            task = Deferred;
            if (!HasDeferred) return false;
            HasDeferred = false;
            return true;
        }

        public void ConsumeKick() => KickConsumed = true;
        public MNG_TaskResult Cancel(MNG_TaskResultKind reason, long tick)
        {
            var result = Result(Current, reason, tick);
            Current = new MNG_PlayerTask { ReceiverSlot = -1, Revision = m_Revision };
            HasDeferred = false;
            KickConsumed = true;
            return result;
        }
        public void Reset()
        {
            m_Revision = -1;
            Current = new MNG_PlayerTask { ReceiverSlot = -1 };
            HasDeferred = false;
            KickConsumed = false;
        }
        public static bool IsKick(MNG_PlayerSkill skill) => skill == MNG_PlayerSkill.AimPass || skill == MNG_PlayerSkill.AimShot;
        static MNG_TaskResult Result(MNG_PlayerTask task, MNG_TaskResultKind kind, long tick)
            => new MNG_TaskResult(kind, tick, task.TaskId, task.ParentCommandId, task.Source);
    }
}
