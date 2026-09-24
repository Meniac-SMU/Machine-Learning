using System;

namespace MachineLearning.Soccer.Manager
{
    /// <summary>Frozen rebuild contract. V1 scenes remain explicitly on their original path.</summary>
    public static class MNG_RuntimeV2
    {
        public const string BehaviorName = "MNG_ManagerV2";
        public const string ObservationSchema = "MNG-OBS-v2-244";
        public const int ObservationSize = 244;
        public const int TaskVersion = 2;
        public const int EventVersion = 2;
        public const int ProtocolVersion = 3;
        public const string EnvironmentRevision = "MNG-MS2-forward-pass-center-20260923";
        public const float CommitmentWatchdogSeconds = 0.15f;
        public const float TaskTimeScale = 3f;
        public const float StallTimeScale = 5f;

        public static void ValidateModelContract(string schema, string behavior, int observations)
        {
            if (schema != ObservationSchema || behavior != BehaviorName || observations != ObservationSize)
                throw new InvalidOperationException("MNG v2 model schema mismatch; v1 models cannot initialize v2.");
        }

        public static void ValidateModelAsset(Unity.InferenceEngine.ModelAsset asset)
        {
            if (asset == null) return;
            var model = Unity.InferenceEngine.ModelLoader.Load(asset);
            foreach (var input in model.inputs)
            {
                if (input.name != "obs_0") continue;
                var shape = input.shape.ToIntArray();
                if (shape.Length == 2 && shape[1] == ObservationSize) return;
                throw new InvalidOperationException("MNG v2 model observation tensor mismatch.");
            }
            throw new InvalidOperationException("MNG v2 model is missing obs_0.");
        }
    }

    public enum MNG_TaskPhase { Idle, Preparing, Committed, Recovering, Receiving }
    public enum MNG_TaskResultKind
    {
        Accepted, RetainedEquivalent, DeferredCommit, RejectedStale, RejectedOwner,
        RejectedInvalidState, CancelledPossessionLost, CancelledMatchState, Expired
    }
    public enum MNG_ActionSource { PolicyCommand, RuleCommand, KeeperTechnique, SafetyEscape }

    public readonly struct MNG_TaskResult
    {
        public readonly MNG_TaskResultKind Kind;
        public readonly long Tick;
        public readonly long TaskId;
        public readonly long ParentCommandId;
        public readonly MNG_ActionSource Source;
        public MNG_TaskResult(MNG_TaskResultKind kind, long tick, long taskId, long parentCommandId,
            MNG_ActionSource source = MNG_ActionSource.PolicyCommand)
        { Kind = kind; Tick = tick; TaskId = taskId; ParentCommandId = parentCommandId; Source = source; }
    }
}
