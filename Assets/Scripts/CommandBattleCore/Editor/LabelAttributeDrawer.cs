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
    /// <summary>
    /// <see cref="LabelAttribute"/> の描画を担う PropertyDrawer。
    /// ラベル文字列だけを差し替えて、描画自体は標準のプロパティ描画へ委ねる。
    /// </summary>
    [CustomPropertyDrawer((typeof(LabelAttribute)))]
    public class LabelAttributeDrawer : PropertyDrawer
    {
        /// <summary>
        /// ラベルを属性の表示名へ置き換えて描画する。
        /// </summary>
        /// <param name="position">描画領域。</param>
        /// <param name="property">対象プロパティ。</param>
        /// <param name="label">既定のラベル。テキストを上書きして使う。</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var labelAttr = (LabelAttribute)attribute;

            label.text = labelAttr.Text;

            EditorGUI.PropertyField(position, property, label, true);
        }

        /// <summary>
        /// 描画に必要な高さを返す。ラベルを変えるだけなので標準の高さをそのまま使う。
        /// 子要素を含めるため includeChildren を true にしている。
        /// </summary>
        /// <param name="property">対象プロパティ。</param>
        /// <param name="label">ラベル。</param>
        /// <returns>描画に必要な高さ。</returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}
