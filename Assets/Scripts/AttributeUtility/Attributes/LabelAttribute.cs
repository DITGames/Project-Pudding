/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file LabelAttribute.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief インスペクタに任意表示名で表示する属性
 * =====================================*/
using UnityEngine;

namespace AttributeUtility
{
    // インスペクタ上のフィールド名を任意の文字列（主に日本語）へ差し替える属性
    // 描画は Editor/LabelAttributeDrawer.cs が担う
    // [SerializeField] private とセットで付け、インスペクタに素の英語フィールド名を出さないのが本リポジトリの規約
    public class LabelAttribute : PropertyAttribute
    {
        // インスペクタに表示する文字列
        public string Text { get; }

        // aText : 表示名
        public LabelAttribute(string text)
        {
            Text = text;
        }

        // コレクション自体に適用するかを指定して生成する
        // リストの各要素ではなくリスト全体のラベルを差し替えたい場合に使う
        // aText : 表示名
        // aApplyToCollection : true ならコレクション自体へ適用する
        public LabelAttribute(string text, bool applyToCollection)
            : base(applyToCollection)
        {
            Text = text;
        }
    }
}
