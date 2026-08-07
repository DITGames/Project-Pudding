/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillEffectDefinitionDrawer.cs
 * @author hqrse
 * @date 2026/08/06
 * @brief PPSkillEffectDefinition の [SerializeReference] フィールド用インスペクタ拡張
 * =====================================*/

using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // PPSkillEffectDefinition 型のフィールド・リスト要素を、
    // 型未選択ならツリーポップアップを開く選択ボタン、選択済みなら BuildString() をラベルにしてフィールド展開する
    [CustomPropertyDrawer(typeof(PPSkillEffectDefinition), true)]
    public class PPSkillEffectDefinitionDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            if (string.IsNullOrEmpty(aProperty.managedReferenceFullTypename))
            {
                var buttonRect = new Rect(aPosition.x, aPosition.y, aPosition.width, EditorGUIUtility.singleLineHeight);
                if (GUI.Button(buttonRect, $"+ {aLabel.text} を選択"))
                {
                    // ポップアップのコールバックは非同期(フレームをまたぐ)ため、プロパティをコピーして保持する
                    var propertyCopy = aProperty.Copy();
                    PPSkillEffectPickerPopup.Show(buttonRect, instance =>
                    {
                        propertyCopy.managedReferenceValue = instance;
                        propertyCopy.serializedObject.ApplyModifiedProperties();
                    });
                }
                return;
            }

            PPManagedReferencePickerUtility.DrawAssignedField(aPosition, aProperty, aLabel);
        }

        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
            => PPManagedReferencePickerUtility.GetPropertyHeight(aProperty);
    }
}
