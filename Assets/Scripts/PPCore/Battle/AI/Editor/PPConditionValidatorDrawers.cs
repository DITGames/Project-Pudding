/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPConditionValidatorDrawers.cs
 * @author hqrse
 * @date 2026/08/07
 * @brief パーティ条件・ユニット条件の PropertyDrawer
 * =====================================*/

using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // PPPartyConditionValidator のフィールド・リスト要素を描画する
    // 型未選択なら型選択ボタン、選択済みなら Description をラベルにして展開する
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
                    PPTypeTreePickerPopup.ShowDerived<PPPartyConditionValidator>(buttonRect,
                        "(条件クラスが見つかりません)", instance =>
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

    // PPUnitConditionValidator のフィールド・リスト要素を描画する
    // 型未選択なら型選択ボタン、選択済みなら Description をラベルにして展開する
    [CustomPropertyDrawer(typeof(PPUnitConditionValidator), true)]
    public class PPUnitConditionValidatorDrawer : PropertyDrawer
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
                    PPTypeTreePickerPopup.ShowDerived<PPUnitConditionValidator>(buttonRect,
                        "(ユニット条件クラスが見つかりません)", instance =>
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
