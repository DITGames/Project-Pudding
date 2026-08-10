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
    // 型未選択ならツリーポップアップ（PPPartyConditionPickerPopup）を開く選択ボタン、
    // 選択済みなら Description をラベルにしてフィールド展開する
    // PPSkillEffectDefinitionDrawer と同じ形。選択済みの描画は PPManagedReferencePickerUtility に委譲する
    [CustomPropertyDrawer(typeof(PPPartyConditionValidator), true)]
    public class PPPartyConditionValidatorDrawer : PropertyDrawer
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
                    PPPartyConditionPickerPopup.Show(buttonRect, instance =>
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
