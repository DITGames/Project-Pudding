/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPFieldLabelAttribute.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief 型側の PropertyDrawer が参照するフィールド表示名を付与する属性
 * =====================================*/

using System;

namespace PPCore
{
    // [SerializeReference] のフィールドへ日本語の表示名を与えるための属性
    //
    // AttributeUtility.LabelAttribute は PropertyAttribute であり、Unity は属性側の PropertyDrawer を
    // 型側の CustomPropertyDrawer より優先する。そのため型選択ツリーを持つフィールド（PPDurationConditionDefinition 等）に
    // [Label] を付けると、型選択そのものができなくなる
    // （リストなら LabelAttribute(text, applyToCollection: true) で回避できるが、単一フィールドでは回避できない）
    //
    // 本属性は PropertyAttribute ではなく素の Attribute のため描画へ介入しない
    // 型側の PropertyDrawer が PropertyDrawer.fieldInfo 経由で読み取り、自前でラベルへ反映する
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class PPFieldLabelAttribute : Attribute
    {
        // インスペクタに表示する文字列
        public string Text { get; }

        // aText : 表示名
        public PPFieldLabelAttribute(string aText)
        {
            Text = aText;
        }
    }
}
