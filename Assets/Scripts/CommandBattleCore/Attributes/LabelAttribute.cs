/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file LabelAttribute.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief インスペクタに任意表示名で表示する属性
 * =====================================*/
using UnityEngine;

namespace CommandBattleCore
{
    /// <summary>
    /// インスペクタ上のフィールド名を任意の文字列（主に日本語）へ差し替える属性。
    /// <para>
    /// 描画は <c>Editor/LabelAttributeDrawer.cs</c> が担う。
    /// <c>[SerializeField] private</c> とセットで付け、
    /// インスペクタに素の英語フィールド名を出さないのが本リポジトリの規約。
    /// </para>
    /// </summary>
    public class LabelAttribute : PropertyAttribute
    {
        /// <summary>インスペクタに表示する文字列。</summary>
        public string Text { get; }

        /// <param name="text">表示名。</param>
        public LabelAttribute(string text)
        {
            Text = text;
        }

        /// <summary>
        /// コレクション自体に適用するかを指定して生成する。
        /// リストの各要素ではなくリスト全体のラベルを差し替えたい場合に使う。
        /// </summary>
        /// <param name="text">表示名。</param>
        /// <param name="applyToCollection">true ならコレクション自体へ適用する。</param>
        public LabelAttribute(string text, bool applyToCollection)
            : base(applyToCollection)
        {
            Text = text;
        }
    }
}
