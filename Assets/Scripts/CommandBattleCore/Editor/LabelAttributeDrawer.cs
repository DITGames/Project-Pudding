/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file LabelAttributeDrawer.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief LabelAttributeの表示クラス
 * =====================================*/

using UnityEditor;
using UnityEngine;

namespace CommandBattleCore
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

            label.text = labelAttr.Text;

            EditorGUI.PropertyField(position, property, label, true);
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
