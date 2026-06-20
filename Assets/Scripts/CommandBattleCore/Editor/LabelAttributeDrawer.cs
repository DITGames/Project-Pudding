/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file LabelAttributeDrawer.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief LabelAttributeの表示クラス
 * =====================================*/
using UnityEditor;
using UnityEngine;

namespace CommandBattleCore
{
    [CustomPropertyDrawer((typeof(LabelAttribute)))]
    public class LabelAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var labelAttr = (LabelAttribute)attribute;
            
            label.text = labelAttr.Text;
            
            EditorGUI.PropertyField(position, property, label, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}