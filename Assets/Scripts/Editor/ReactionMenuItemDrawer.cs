using VTuberReactionControllerDemo.Controller;
using UnityEditor;
using UnityEngine;

namespace VTuberReactionControllerDemo.Editor
{
    [CustomPropertyDrawer(typeof(ReactionCatalog.ReactionMenuItem))]
    internal sealed class ReactionMenuItemDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty displayNameProperty = property.FindPropertyRelative("displayName");
            SerializedProperty keyProperty = property.FindPropertyRelative("key");
            SerializedProperty reactionEventProperty = property.FindPropertyRelative("reactionEvent");

            label.text = CreateLabel(displayNameProperty, keyProperty, reactionEventProperty, label.text);

            EditorGUI.BeginProperty(position, label, property);

            Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                float y = foldoutRect.yMax + Spacing;
                DrawProperty(ref y, position, displayNameProperty, "Name", false);
                DrawProperty(ref y, position, keyProperty, "Key", false);
                DrawProperty(ref y, position, reactionEventProperty, "Reaction Event", true);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            SerializedProperty reactionEventProperty = property.FindPropertyRelative("reactionEvent");
            return EditorGUIUtility.singleLineHeight
                + Spacing
                + EditorGUIUtility.singleLineHeight
                + Spacing
                + EditorGUIUtility.singleLineHeight
                + Spacing
                + EditorGUI.GetPropertyHeight(reactionEventProperty, true);
        }

        private static void DrawProperty(
            ref float y,
            Rect position,
            SerializedProperty property,
            string label,
            bool includeChildren)
        {
            float height = EditorGUI.GetPropertyHeight(property, includeChildren);
            Rect rect = new Rect(position.x, y, position.width, height);
            EditorGUI.PropertyField(rect, property, new GUIContent(label), includeChildren);
            y += height + Spacing;
        }

        private static string CreateLabel(
            SerializedProperty displayNameProperty,
            SerializedProperty keyProperty,
            SerializedProperty reactionEventProperty,
            string fallback)
        {
            if (!string.IsNullOrWhiteSpace(displayNameProperty.stringValue))
            {
                return displayNameProperty.stringValue;
            }

            string keyName = keyProperty.enumValueIndex >= 0
                && keyProperty.enumValueIndex < keyProperty.enumDisplayNames.Length
                    ? keyProperty.enumDisplayNames[keyProperty.enumValueIndex]
                    : "Key";
            SerializedProperty typeProperty = reactionEventProperty.FindPropertyRelative("Type");
            string reactionType = typeProperty != null && !string.IsNullOrWhiteSpace(typeProperty.stringValue)
                ? typeProperty.stringValue
                : "reaction";

            if (string.IsNullOrWhiteSpace(keyName) && string.IsNullOrWhiteSpace(reactionType))
            {
                return fallback;
            }

            return $"{keyName} -> {reactionType}";
        }
    }
}
