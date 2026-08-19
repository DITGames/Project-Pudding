/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PercentLabelDrawer.cs
 * @author hqrse
 * @date 2026/07/30
 * @brief PercentLabelAttributeの表示クラス
 * =====================================*/

using UnityEditor;
using UnityEngine;

namespace AttributeUtility
{
    // PercentLabelAttribute の描画を担う PropertyDrawer
    // ラベルに現在値のパーセント表記を足したうえで、値は 0〜1 のスライダーとして編集させる
    // ラベルは毎回の描画で組み立てるため、スライダーを動かすと表示も追従する
    [CustomPropertyDrawer(typeof(PercentLabelAttribute))]
    public class PercentLabelDrawer : PropertyDrawer
    {
        // ラベルへパーセント表記を併記してスライダーを描画する
        // float 以外のプロパティに付けられていた場合は、誤解を招かないよう警告表示に切り替える
        // position : 描画領域
        // property : 対象プロパティ
        // label : 既定のラベル
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var percentAttr = (PercentLabelAttribute)attribute;

            if (property.propertyType != SerializedPropertyType.Float)
            {
                EditorGUI.LabelField(position, label.text, "PercentLabel は float 専用です");
                return;
            }

            label.text = BuildLabelText(percentAttr, property.floatValue);
            EditorGUI.Slider(position, property, percentAttr.Min, percentAttr.Max, label);
        }

        // 表示名にパーセント表記（または 0 のときの番兵用文字列）を付けたラベル文字列を組み立てる
        // aAttr : 対象の属性
        // aValue : 現在値
        // return : ラベルに表示する文字列
        private static string BuildLabelText(PercentLabelAttribute aAttr, float aValue)
        {
            if (aAttr.ZeroText != null && Mathf.Approximately(aValue, 0f))
            {
                return $"{aAttr.Text} ({aAttr.ZeroText})";
            }
            return $"{aAttr.Text} {aValue * 100f:0.#}%";
        }

        // 描画に必要な高さを返す。1 行のスライダーなので標準の行高を使う
        // property : 対象プロパティ
        // label : ラベル
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;
    }
}
