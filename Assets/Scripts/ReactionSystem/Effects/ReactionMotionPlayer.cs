using System.Collections;
using UnityEngine;

namespace VTuberReactionControllerDemo.Controller
{
    public sealed class ReactionMotionPlayer : MonoBehaviour
    {
        [Header("Bone Targets")]
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private bool autoBindHumanoidBones = true;
        [SerializeField] private Transform head;
        [SerializeField] private Transform neck;
        [SerializeField] private Transform chest;

        [Header("Impact Rotation")]
        [Tooltip("Maximum head local rotation when the motion amount reaches 1.0. The curve and spring calculate the amount, not the final degrees.")]
        [SerializeField] private Vector3 headImpactEuler = new Vector3(24f, 0f, 0f);
        [Tooltip("Maximum neck local rotation when the motion amount reaches 1.0.")]
        [SerializeField] private Vector3 neckImpactEuler = new Vector3(10f, 0f, 0f);
        [Tooltip("Maximum chest local rotation when the motion amount reaches 1.0.")]
        [SerializeField] private Vector3 chestImpactEuler = new Vector3(4f, 0f, 0f);
        [Tooltip("Duration used to evaluate the impact curve. A curve key at 0.28 means 28% of this duration, not 0.28%.")]
        [SerializeField, Min(0.01f)] private float impactSeconds = 0.24f;
        [Tooltip("Normalized target amount over impactSeconds. The default reaches full impact at 28% of the duration, then returns the target to 0 by the end.")]
        [SerializeField] private AnimationCurve impactCurve = CreateDefaultImpactCurve();

        [Header("Spring Return")]
        [Tooltip("How strongly currentAmount accelerates toward targetAmount.")]
        [SerializeField, Min(0.01f)] private float springStrength = 90f;
        [Tooltip("Frame-rate independent velocity damping. Higher values reduce bounce and overshoot faster.")]
        [SerializeField, Min(0.01f)] private float damping = 16f;
        [SerializeField, Min(0.01f)] private float maxSettleSeconds = 0.7f;
        [SerializeField, Min(0f)] private float settleThreshold = 0.0025f;

        private Quaternion headBaseRotation;
        private Quaternion neckBaseRotation;
        private Quaternion chestBaseRotation;
        private float targetAmount;
        private float currentAmount;
        private float amountVelocity;
        private bool isPlaying;
        private bool hasStoredRotations;

        private bool HasAnyTarget => head != null || neck != null || chest != null;

        private void Awake()
        {
            EnsureInitialized(true);
        }

        private void Reset()
        {
            EnsureInitialized(false);
        }

        private void LateUpdate()
        {
            if (!hasStoredRotations)
            {
                return;
            }

            if (!isPlaying && Mathf.Abs(currentAmount) <= settleThreshold && Mathf.Abs(amountVelocity) <= settleThreshold)
            {
                return;
            }

            UpdateSpring(Time.deltaTime);
            ApplyMotionOffset(currentAmount);
        }

        private void OnDisable()
        {
            ResetMotion();
        }

        public IEnumerator Play()
        {
            // Resolve the configured Animator bones before starting this reaction.
            EnsureInitialized(true);

            if (!HasAnyTarget)
            {
                Debug.LogWarning("ReactionMotionPlayer could not find head, neck, or chest targets.", this);
                yield break;
            }

            // Store the current pose so the procedural offset is always relative to the avatar's pose at impact time.
            StoreBaseRotations();
            isPlaying = true;
            float elapsedSeconds = 0f;

            // Impact phase: the curve defines the target amount over normalized time.
            // LateUpdate applies the spring-smoothed currentAmount to the bones each frame.
            while (elapsedSeconds < impactSeconds)
            {
                elapsedSeconds += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsedSeconds / impactSeconds);
                targetAmount = impactCurve != null ? impactCurve.Evaluate(normalizedTime) : normalizedTime;
                yield return null;
            }

            // Return phase: the target becomes the original pose, then the spring is allowed to settle.
            targetAmount = 0f;
            float settleElapsedSeconds = 0f;

            while (settleElapsedSeconds < maxSettleSeconds && !IsSettled())
            {
                settleElapsedSeconds += Time.deltaTime;
                yield return null;
            }

            // Restore exact base rotations and clear spring state so the next reaction starts cleanly.
            ResetMotion();
        }

        private void EnsureInitialized(bool logWarnings)
        {
            if (!autoBindHumanoidBones)
            {
                return;
            }

            if (targetAnimator == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("ReactionMotionPlayer requires a Target Animator before it can bind humanoid bones.", this);
                }

                return;
            }

            if (!targetAnimator.isHuman)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("ReactionMotionPlayer Target Animator must be a Humanoid Animator.", this);
                }

                return;
            }

            if (head == null)
            {
                head = targetAnimator.GetBoneTransform(HumanBodyBones.Head);
            }

            if (neck == null)
            {
                neck = targetAnimator.GetBoneTransform(HumanBodyBones.Neck);
            }

            if (chest == null)
            {
                chest = targetAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
            }

            if (chest == null)
            {
                chest = targetAnimator.GetBoneTransform(HumanBodyBones.Chest);
            }
        }

        private void StoreBaseRotations()
        {
            if (head != null)
            {
                headBaseRotation = head.localRotation;
            }

            if (neck != null)
            {
                neckBaseRotation = neck.localRotation;
            }

            if (chest != null)
            {
                chestBaseRotation = chest.localRotation;
            }

            hasStoredRotations = true;
        }

        private void UpdateSpring(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            // Semi-implicit spring step with unit mass:
            // acceleration comes from the distance to the target amount,
            // velocity is reduced by exponential damping so it behaves consistently across frame rates,
            // and currentAmount becomes the final multiplier used by ApplyMotionOffset.
            float acceleration = (targetAmount - currentAmount) * springStrength;
            amountVelocity += acceleration * deltaTime;
            amountVelocity *= Mathf.Exp(-damping * deltaTime);
            currentAmount += amountVelocity * deltaTime;
        }

        private void ApplyMotionOffset(float amount)
        {
            if (head != null)
            {
                head.localRotation = headBaseRotation * Quaternion.Euler(headImpactEuler * amount);
            }

            if (neck != null)
            {
                neck.localRotation = neckBaseRotation * Quaternion.Euler(neckImpactEuler * amount);
            }

            if (chest != null)
            {
                chest.localRotation = chestBaseRotation * Quaternion.Euler(chestImpactEuler * amount);
            }
        }

        private bool IsSettled()
        {
            return Mathf.Abs(currentAmount) <= settleThreshold && Mathf.Abs(amountVelocity) <= settleThreshold;
        }

        private void ResetMotion()
        {
            if (hasStoredRotations)
            {
                ApplyMotionOffset(0f);
            }

            targetAmount = 0f;
            currentAmount = 0f;
            amountVelocity = 0f;
            isPlaying = false;
            hasStoredRotations = false;
        }

        private static AnimationCurve CreateDefaultImpactCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.28f, 1f),
                new Keyframe(1f, 0f));
        }
    }
}
