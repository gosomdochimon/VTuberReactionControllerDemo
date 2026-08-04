using System;
using UnityEngine;
using VRM;

namespace VTuberReactionControllerDemo.Controller
{
    [CreateAssetMenu(
        fileName = "ReactionDefinition",
        menuName = "VTuber Reaction Controller/Reaction Definition")]
    public sealed class ReactionDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string type;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;

        [Header("VFX")]
        [SerializeField] private GameObject vfxPrefab;
        [SerializeField] private Vector3 vfxLocalOffset;
        [SerializeField, Min(0f)] private float vfxLifetimeSeconds = 4f;
        [SerializeField] private bool parentVfxToSpawnPoint;

        [Header("Expression")]
        [SerializeField] private ReactionExpressionKeyMode expressionKeyMode = ReactionExpressionKeyMode.Preset;
        [SerializeField] private BlendShapePreset expressionPreset = BlendShapePreset.Joy;
        [SerializeField] private string customExpressionName;

        [Header("Special Reaction")]
        [SerializeField] private bool playThrowReaction;

        public string Type => type;
        public string DisplayName => displayName;
        public string Description => description;
        public GameObject VfxPrefab => vfxPrefab;
        public Vector3 VfxLocalOffset => vfxLocalOffset;
        public float VfxLifetimeSeconds => vfxLifetimeSeconds;
        public bool ParentVfxToSpawnPoint => parentVfxToSpawnPoint;
        public bool PlaysThrowReaction => playThrowReaction;
        public bool HasVfxPrefab => vfxPrefab != null;
        public bool HasExpressionKey => expressionKeyMode == ReactionExpressionKeyMode.Preset
            || !string.IsNullOrWhiteSpace(customExpressionName);

        public bool Matches(string reactionType)
        {
            return string.Equals(type, reactionType, StringComparison.OrdinalIgnoreCase);
        }

        public BlendShapeKey CreateExpressionKey()
        {
            if (expressionKeyMode == ReactionExpressionKeyMode.CustomName)
            {
                return BlendShapeKey.CreateUnknown(customExpressionName);
            }

            return BlendShapeKey.CreateFromPreset(expressionPreset);
        }
    }
}
