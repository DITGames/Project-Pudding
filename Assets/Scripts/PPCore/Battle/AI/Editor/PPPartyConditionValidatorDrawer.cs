/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyConditionValidatorDrawer.cs
 * @author hqrse
 * @date 2026/08/07
 * @brief PPPartyConditionValidator の [SerializeReference] フィールド用インスペクタ拡張
 * =====================================*/

using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // PPPartyConditionValidator 型のフィールド・リスト要素を、
    // 型未選択なら型選択メニューを開くボタン、選択済みなら Description をラベルにしてフィールド展開する
    // PPSkillEffectDefinitionDrawer と同じ形で PPManagedReferencePickerUtility に委譲する
    [CustomPropertyDrawer(typeof(PPPartyConditionValidator), true)]
    public class PPPartyConditionValidatorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
            => PPManagedReferencePickerUtility.OnGUI(aPosition, aProperty, aLabel, typeof(PPPartyConditionValidator));

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
            => PPManagedReferencePickerUtility.GetPropertyHeight(aProperty);
    }
}
