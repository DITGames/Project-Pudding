/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPEffectDefinitionDrawer.cs
 * @author hqrse
 * @date 2026/08/06
 * @brief PPEffectDefinition の [SerializeReference] フィールド用インスペクタ拡張
 * =====================================*/

using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // PPEffectDefinition 型のフィールドを、型未選択なら選択ボタン、選択済みならそのままフィールド展開する
    [CustomPropertyDrawer(typeof(PPEffectDefinition), true)]
    public class PPEffectDefinitionDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
            => PPManagedReferencePickerUtility.OnGUI(aPosition, aProperty, aLabel, typeof(PPEffectDefinition));

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
            => PPManagedReferencePickerUtility.GetPropertyHeight(aProperty);
    }
}
