/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file LabelAttributeDrawer.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief LabelAttributeの表示クラス
 * =====================================*/

using UnityEditor;
using UnityEngine;

namespace AttributeUtility
{
    // LabelAttribute の描画を担う PropertyDrawer
    // ラベル文字列だけを差し替えて、描画自体は標準のプロパティ描画へ委ねる
    [CustomPropertyDrawer((typeof(LabelAttribute)))]
    public class LabelAttributeDrawer : PropertyDrawer
    {
        // ラベルと入力欄のあいだに空ける余白
        private const float LabelMargin = 8f;
        // ラベルへ回してよい横幅の上限。入力欄が潰れない範囲に留めるためのもの
        private const float MaxLabelRatio = 0.6f;

        // ラベルを属性の表示名へ置き換えて描画する
        // position : 描画領域
        // property : 対象プロパティ
        // label : 既定のラベル。テキストを上書きして使う
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var labelAttr = (LabelAttribute)attribute;

            // Unityが渡すlabelは描画パス内で使い回される共有インスタンスのことがあるため、
            // .textを直接書き換えず新しいGUIContentを作る(直接書き換えると他フィールドの表示に文字列が漏れ出す)
            var displayLabel = new GUIContent(labelAttr.Text, label.image, label.tooltip);

            // 日本語の表示名は既定のラベル幅に収まらないことがあり、収まらないと途中で切れて読めなくなる
            // このフィールドの間だけラベル幅を広げ、描き終えたら元へ戻す
            float previousWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = ResolveLabelWidth(position, displayLabel, previousWidth);

            EditorGUI.PropertyField(position, property, displayLabel, true);

            EditorGUIUtility.labelWidth = previousWidth;
        }

        // 表示名が収まるラベル幅を求める
        // 既定の幅で収まっていればそのまま使い、はみ出す場合だけ広げる
        // 常に広げないのは、揃っている列をむやみに崩さないため
        // aPosition : 描画領域
        // aLabel : 表示するラベル
        // aCurrentWidth : 現在のラベル幅
        // return : 使用するラベル幅
        private static float ResolveLabelWidth(Rect aPosition, GUIContent aLabel, float aCurrentWidth)
        {
            float required = EditorStyles.label.CalcSize(aLabel).x + LabelMargin;
            if (required <= aCurrentWidth) return aCurrentWidth;

            // 入力欄が潰れるほどは広げない。上限に当たった場合は従来どおり切り詰められる
            float limit = Mathf.Max(aCurrentWidth, aPosition.width * MaxLabelRatio);
            return Mathf.Min(required, limit);
        }

        // 描画に必要な高さを返す。ラベルを変えるだけなので標準の高さをそのまま使う
        // 子要素を含めるため includeChildren を true にしている
        // property : 対象プロパティ
        // label : ラベル
        // return : 描画に必要な高さ
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}
