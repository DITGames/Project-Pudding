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
    // 条件アセットの参照フィールドを、アセット名ではなく説明文で表示する PropertyDrawer
    // 条件リストが「Element 0, Element 1...」ではなく
    // 「HPが30%以下」のように読める形になり、ルールの中身が一覧で把握できる
    [CustomPropertyDrawer(typeof(PPPartyConditionValidator), true)]
    public class PPPartyConditionValidatorDrawer : PropertyDrawer
    {
        // 説明文が設定されていればラベルとして使い、無ければ既定のラベルのまま描画する
        // aPosition : 描画領域
        // aProperty : 対象プロパティ
        // aLabel : 既定のラベル
        public override void OnGUI(Rect aPosition, SerializedProperty aProperty, GUIContent aLabel)
        {
            var condition = aProperty.objectReferenceValue as PPPartyConditionValidator;
            var label = (condition != null && !string.IsNullOrEmpty(condition.Description))
                ? new GUIContent(condition.Description, aLabel.tooltip)
                : aLabel;

            EditorGUI.PropertyField(aPosition, aProperty, label);
        }

        // 描画に必要な高さを返す。ラベルを差し替えるだけなので標準の高さを使う
        // aProperty : 対象プロパティ
        // aLabel : ラベル
        public override float GetPropertyHeight(SerializedProperty aProperty, GUIContent aLabel)
            => EditorGUI.GetPropertyHeight(aProperty, aLabel, true);
    }
}
