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

            EditorGUI.PropertyField(position, property, displayLabel, true);
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
