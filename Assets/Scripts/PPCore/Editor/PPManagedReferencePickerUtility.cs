/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPManagedReferencePickerUtility.cs
 * @author hqrse
 * @date 2026/08/06
 * @brief [SerializeReference] フィールドの型選択・インライン編集を共通化するエディタ用ユーティリティ
 * =====================================*/

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // [SerializeReference] フィールドの「型未選択なら選択ボタン、選択済みならフィールド展開」を共通化する
    // aBaseType の具象派生型を TypeCache から集め、GenericMenu で選ばせる
    // 選択時は Activator.CreateInstance でインスタンスを生成しその場で managedReferenceValue に設定する
    public static class PPManagedReferencePickerUtility
    {
        // aPosition : 描画領域
        // aProperty : 描画対象の SerializeReference プロパティ
        // aLabel : フィールドのラベル
        // aBaseType : 選択候補を絞り込む基底型
        public static void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel, Type aBaseType)
        {
            if (string.IsNullOrEmpty(aProperty.managedReferenceFullTypename))
            {
                var buttonRect = new Rect(aPosition.x, aPosition.y, aPosition.width, EditorGUIUtility.singleLineHeight);
                if (GUI.Button(buttonRect, $"+ {aLabel.text} を選択"))
                {
                    ShowTypeMenu(aProperty, aBaseType);
                }
                return;
            }

            DrawAssignedField(aPosition, aProperty, aLabel);
        }

        // aProperty : 高さを求める対象の SerializeReference プロパティ
        public static float GetPropertyHeight(SerializedProperty aProperty)
            => string.IsNullOrEmpty(aProperty.managedReferenceFullTypename)
                ? EditorGUIUtility.singleLineHeight
                : EditorGUI.GetPropertyHeight(aProperty, true);

        // 選択済みの SerializeReference プロパティを、BuildString() の戻り値をラベルにして描画する
        // ツリーピッカー（PPSkillEffectPickerPopup）経由で設定された場合の描画にも共通で使う
        // aPosition : 描画領域
        // aProperty : 描画対象の SerializeReference プロパティ（型選択済み）
        // aLabel : フィールドの既定ラベル
        public static void DrawAssignedField(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            var label = new GUIContent(aLabel);
            switch (aProperty.managedReferenceValue)
            {
                case PPSkillEffectDefinition skillEffect: label.text = skillEffect.BuildString(); break;
                case PPEffectDefinition statusEffect: label.text = statusEffect.BuildString(); break;
                case PPPartyConditionValidator condition when !string.IsNullOrEmpty(condition.Description):
                    label.text = condition.Description; break;
            }
            EditorGUI.PropertyField(aPosition, aProperty, label, true);
        }

        // aBaseType の具象派生型一覧をメニュー表示し、選ばれた型のインスタンスを aProperty に設定する
        // aProperty : 設定先の SerializeReference プロパティ
        // aBaseType : 選択候補を絞り込む基底型
        private static void ShowTypeMenu(SerializedProperty aProperty, Type aBaseType)
        {
            var menu = new GenericMenu();
            foreach (var type in TypeCache.GetTypesDerivedFrom(aBaseType))
            {
                if (type.IsAbstract) continue;

                var path = type.GetCustomAttribute<PPTypeMenuNameAttribute>()?.Path
                                   ?? ObjectNames.NicifyVariableName(type.Name);

                menu.AddItem(new GUIContent(path), false, () =>
                {
                    aProperty.managedReferenceValue = Activator.CreateInstance(type);
                    aProperty.serializedObject.ApplyModifiedProperties();
                });
            }
            menu.ShowAsContext();
        }
    }
}
