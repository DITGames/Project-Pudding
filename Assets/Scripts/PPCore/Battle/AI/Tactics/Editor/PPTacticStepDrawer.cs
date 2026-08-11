/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTacticStepDrawer.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief PPTacticStepBase の [SerializeReference] フィールド用インスペクタ拡張
 * =====================================*/

using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // PPTacticStepBase 型のフィールド・リスト要素を、
    // 型未選択ならツリーポップアップ（PPTypeTreePickerPopup）を開く選択ボタン、
    // 選択済みなら Description をラベルにしてフィールド展開する
    // 条件クラスの選択と同じ操作感に揃えている
    [CustomPropertyDrawer(typeof(PPTacticStepBase), true)]
    public class PPTacticStepDrawer : PropertyDrawer
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
                    PPTypeTreePickerPopup.ShowDerived<PPTacticStepBase>(buttonRect,
                        "(戦術ステップが見つかりません)", instance =>
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
