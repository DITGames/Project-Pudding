/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyConditionValidatorDrawer.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief 条件リストの要素ラベルにDescriptionを表示するDrawer
 * =====================================*/
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    [CustomPropertyDrawer(typeof(PPPartyConditionValidator), true)]
    public class PPPartyConditionValidatorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            var condition = aProperty.objectReferenceValue as PPPartyConditionValidator;
            var label = (condition != null && !string.IsNullOrEmpty(condition.Description))
                ? new GUIContent(condition.Description, aLabel.tooltip)
                : aLabel;
            
            EditorGUI.PropertyField(aPosition, aProperty, label);
        }
        
        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
            => EditorGUI.GetPropertyHeight(aProperty, aLabel, true);
    }
}