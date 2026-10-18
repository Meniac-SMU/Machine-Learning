using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    [DisallowMultipleComponent]
    public sealed class FiveStamina : MonoBehaviour
    {
        [SerializeField, Range(0,1)] float value = 1;
        public float Value => value;
        public void ResetForMatch() => value = 1;
        public void SetForScenario(float amount) => value = Mathf.Clamp01(amount);
        public static float SpeedFactor(float amount, bool dribbling)
            => 1f - (amount <= .4f ? .1f : amount <= .7f ? .05f : 0f) - (dribbling ? .1f : 0f);
        public void Advance(float actualSpeed, float seconds, FiveProfile profile)
        {
            if (!float.IsFinite(actualSpeed) || !float.IsFinite(seconds) || actualSpeed < 0 || seconds < 0)
                throw new System.ArgumentOutOfRangeException();
            var recovery = actualSpeed <= profile.slowRecoverySpeed ? profile.recoveryPerSecond : 0;
            value = Mathf.Clamp01(value + (recovery - actualSpeed * profile.staminaPerMetre) * seconds);
        }
    }
}
