using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRM;

namespace VTuberReactionControllerDemo.Controller
{
    public sealed class ReactionController : MonoBehaviour
    {
        [Header("Queue")]
        [SerializeField, Min(0f)] private float reactionIntervalSeconds = 0.75f;

        [Header("VFX")]
        [SerializeField] private Transform vfxSpawnPoint;
        [SerializeField] private ReactionVfxBinding[] vfxBindings = Array.Empty<ReactionVfxBinding>();
        [SerializeField] private float fallbackVfxLifetimeSeconds = 4f;

        [Header("Expression")]
        [SerializeField] private VRMBlendShapeProxy expressionProxy;
        [SerializeField] private ReactionExpressionBinding[] expressionBindings = Array.Empty<ReactionExpressionBinding>();
        [SerializeField, Range(0f, 1f)] private float expressionWeight = 1f;
        [SerializeField, Min(0f)] private float expressionHoldSeconds = 1.25f;

        private readonly Queue<ReactionEvent> pendingReactions = new Queue<ReactionEvent>();
        private Coroutine processQueueCoroutine;
        private BlendShapeKey activeExpressionKey;
        private bool hasActiveExpression;

        public int PendingReactionCount => pendingReactions.Count;
        public bool IsProcessing => processQueueCoroutine != null;

        public void Enqueue(ReactionEvent reactionEvent)
        {
            if (reactionEvent == null)
            {
                return;
            }

            pendingReactions.Enqueue(reactionEvent);
            TryStartProcessingQueue();
        }

        private void OnEnable()
        {
            TryStartProcessingQueue();
        }

        private void OnDisable()
        {
            if (processQueueCoroutine != null)
            {
                StopCoroutine(processQueueCoroutine);
                processQueueCoroutine = null;
            }

            ResetActiveExpression();
            pendingReactions.Clear();
        }

        private void TryStartProcessingQueue()
        {
            if (!isActiveAndEnabled || processQueueCoroutine != null || pendingReactions.Count == 0)
            {
                return;
            }

            processQueueCoroutine = StartCoroutine(ProcessReactionQueue());
        }

        private IEnumerator ProcessReactionQueue()
        {
            while (pendingReactions.Count > 0)
            {
                ReactionEvent reactionEvent = pendingReactions.Dequeue();
                yield return PlayReaction(reactionEvent);

                if (pendingReactions.Count > 0)
                {
                    yield return CreateQueueDelay();
                }
            }

            processQueueCoroutine = null;
        }

        private YieldInstruction CreateQueueDelay()
        {
            if (reactionIntervalSeconds <= 0f)
            {
                return null;
            }

            return new WaitForSeconds(reactionIntervalSeconds);
        }

        private IEnumerator PlayReaction(ReactionEvent reactionEvent)
        {
            PlayVfx(reactionEvent.Type);
            WriteLog(reactionEvent.Message);

            ReactionExpressionBinding binding = FindExpressionBinding(reactionEvent.Type);
            if (binding == null || expressionProxy == null || !binding.HasExpressionKey)
            {
                yield break;
            }

            ApplyExpression(binding, expressionWeight);

            if (expressionHoldSeconds > 0f)
            {
                yield return new WaitForSeconds(expressionHoldSeconds);
            }

            ResetActiveExpression();
        }

        private void PlayVfx(string reactionType)
        {
            ReactionVfxBinding binding = FindVfxBinding(reactionType);
            if (binding == null || binding.Prefab == null)
            {
                return;
            }

            Transform spawnPoint = vfxSpawnPoint != null ? vfxSpawnPoint : transform;
            Vector3 spawnPosition = spawnPoint.TransformPoint(binding.LocalOffset);
            Quaternion spawnRotation = spawnPoint.rotation;
            GameObject vfxInstance = Instantiate(binding.Prefab, spawnPosition, spawnRotation);

            if (binding.ParentToSpawnPoint)
            {
                vfxInstance.transform.SetParent(spawnPoint, true);
            }

            float lifetimeSeconds = binding.LifetimeSeconds > 0f
                ? binding.LifetimeSeconds
                : fallbackVfxLifetimeSeconds;

            Destroy(vfxInstance, lifetimeSeconds);
        }

        private ReactionVfxBinding FindVfxBinding(string reactionType)
        {
            if (string.IsNullOrWhiteSpace(reactionType))
            {
                return null;
            }

            for (int i = 0; i < vfxBindings.Length; i++)
            {
                ReactionVfxBinding binding = vfxBindings[i];
                if (binding != null && binding.Matches(reactionType))
                {
                    return binding;
                }
            }

            return null;
        }

        private ReactionExpressionBinding FindExpressionBinding(string reactionType)
        {
            if (string.IsNullOrWhiteSpace(reactionType))
            {
                return null;
            }

            for (int i = 0; i < expressionBindings.Length; i++)
            {
                ReactionExpressionBinding binding = expressionBindings[i];
                if (binding != null && binding.Matches(reactionType))
                {
                    return binding;
                }
            }

            return null;
        }

        private void ApplyExpression(ReactionExpressionBinding binding, float weight)
        {
            if (expressionProxy == null || binding == null || !binding.HasExpressionKey)
            {
                return;
            }

            ResetActiveExpression();

            activeExpressionKey = binding.CreateKey();
            hasActiveExpression = true;
            expressionProxy.ImmediatelySetValue(activeExpressionKey, Mathf.Clamp01(weight));
        }

        private void ResetActiveExpression()
        {
            if (!hasActiveExpression || expressionProxy == null)
            {
                hasActiveExpression = false;
                return;
            }

            expressionProxy.ImmediatelySetValue(activeExpressionKey, 0f);
            hasActiveExpression = false;
        }

        private void WriteLog(string text)
        {
            Debug.Log(text);
        }

        [Serializable]
        private sealed class ReactionVfxBinding
        {
            [SerializeField] private string type;
            [SerializeField] private GameObject prefab;
            [SerializeField] private Vector3 localOffset;
            [SerializeField] private float lifetimeSeconds = 4f;
            [SerializeField] private bool parentToSpawnPoint;

            public GameObject Prefab => prefab;
            public Vector3 LocalOffset => localOffset;
            public float LifetimeSeconds => lifetimeSeconds;
            public bool ParentToSpawnPoint => parentToSpawnPoint;

            public bool Matches(string reactionType)
            {
                return string.Equals(type, reactionType, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Serializable]
        private sealed class ReactionExpressionBinding
        {
            [SerializeField] private string type;
            [SerializeField] private ExpressionKeyMode keyMode = ExpressionKeyMode.Preset;
            [SerializeField] private BlendShapePreset preset = BlendShapePreset.Joy;
            [SerializeField] private string customName;

            public bool HasExpressionKey => keyMode == ExpressionKeyMode.Preset || !string.IsNullOrWhiteSpace(customName);

            public BlendShapeKey CreateKey()
            {
                if (keyMode == ExpressionKeyMode.CustomName)
                {
                    return BlendShapeKey.CreateUnknown(customName);
                }

                return BlendShapeKey.CreateFromPreset(preset);
            }

            public bool Matches(string reactionType)
            {
                return string.Equals(type, reactionType, StringComparison.OrdinalIgnoreCase);
            }
        }

        private enum ExpressionKeyMode
        {
            Preset,
            CustomName
        }
    }
}
