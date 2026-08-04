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

        [Header("Reaction Definitions")]
        [SerializeField] private ReactionDefinition[] reactionDefinitions = Array.Empty<ReactionDefinition>();

        [Header("VFX")]
        [SerializeField] private Transform vfxSpawnPoint;
        [SerializeField] private float fallbackVfxLifetimeSeconds = 4f;

        [Header("Expression")]
        [SerializeField] private VRMBlendShapeProxy expressionProxy;
        [SerializeField, Range(0f, 1f)] private float expressionWeight = 1f;
        [SerializeField, Min(0f)] private float expressionHoldSeconds = 1.25f;

        [Header("Special Reactions")]
        [SerializeField] private ThrowReactionPlayer throwReactionPlayer;

        private readonly Queue<ReactionEvent> pendingReactions = new Queue<ReactionEvent>();
        private Coroutine processQueueCoroutine;
        private BlendShapeKey activeExpressionKey;
        private bool hasActiveExpression;

        public event Action<ReactionEvent> ReactionStarted;

        public int PendingReactionCount => pendingReactions.Count;
        public bool IsProcessing => processQueueCoroutine != null;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Reset()
        {
            EnsureInitialized();
        }

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
            EnsureInitialized();
            TryStartProcessingQueue();
        }

        private void EnsureInitialized()
        {
            if (throwReactionPlayer == null)
            {
                throwReactionPlayer = GetComponent<ThrowReactionPlayer>();
            }
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
            ReactionDefinition definition = FindReactionDefinition(reactionEvent.Type);

            ReactionStarted?.Invoke(reactionEvent);
            PlayVfx(definition);
            WriteLog(reactionEvent.Message);

            if (ShouldPlayThrowReaction(definition) && throwReactionPlayer != null)
            {
                yield return throwReactionPlayer.Play();
            }

            if (!TryGetExpressionKey(definition, out BlendShapeKey expressionKey) || expressionProxy == null)
            {
                yield break;
            }

            ApplyExpression(expressionKey, expressionWeight);

            if (expressionHoldSeconds > 0f)
            {
                yield return new WaitForSeconds(expressionHoldSeconds);
            }

            ResetActiveExpression();
        }

        private void PlayVfx(ReactionDefinition definition)
        {
            if (definition == null || !definition.HasVfxPrefab)
            {
                return;
            }

            PlayVfx(
                definition.VfxPrefab,
                definition.VfxLocalOffset,
                definition.VfxLifetimeSeconds,
                definition.ParentVfxToSpawnPoint);
        }

        private void PlayVfx(GameObject prefab, Vector3 localOffset, float configuredLifetimeSeconds, bool parentToSpawnPoint)
        {
            if (prefab == null)
            {
                return;
            }

            Transform spawnPoint = vfxSpawnPoint != null ? vfxSpawnPoint : transform;
            Vector3 spawnPosition = spawnPoint.TransformPoint(localOffset);
            Quaternion spawnRotation = spawnPoint.rotation;
            GameObject vfxInstance = Instantiate(prefab, spawnPosition, spawnRotation);

            if (parentToSpawnPoint)
            {
                vfxInstance.transform.SetParent(spawnPoint, true);
            }

            float lifetimeSeconds = configuredLifetimeSeconds > 0f
                ? configuredLifetimeSeconds
                : fallbackVfxLifetimeSeconds;

            Destroy(vfxInstance, lifetimeSeconds);
        }

        private ReactionDefinition FindReactionDefinition(string reactionType)
        {
            if (string.IsNullOrWhiteSpace(reactionType))
            {
                return null;
            }

            if (reactionDefinitions == null)
            {
                return null;
            }

            for (int i = 0; i < reactionDefinitions.Length; i++)
            {
                ReactionDefinition definition = reactionDefinitions[i];
                if (definition != null && definition.Matches(reactionType))
                {
                    return definition;
                }
            }

            return null;
        }

        private static bool TryGetExpressionKey(ReactionDefinition definition, out BlendShapeKey expressionKey)
        {
            if (definition != null && definition.HasExpressionKey)
            {
                expressionKey = definition.CreateExpressionKey();
                return true;
            }

            expressionKey = default;
            return false;
        }

        private static bool ShouldPlayThrowReaction(ReactionDefinition definition)
        {
            return definition != null && definition.PlaysThrowReaction;
        }

        private void ApplyExpression(BlendShapeKey expressionKey, float weight)
        {
            if (expressionProxy == null)
            {
                return;
            }

            ResetActiveExpression();

            activeExpressionKey = expressionKey;
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

    }
}
