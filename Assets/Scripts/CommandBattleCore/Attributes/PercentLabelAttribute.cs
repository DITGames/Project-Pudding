/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PercentLabelAttribute.cs
 * @author hqrse
 * @date 2026/07/30
 * @brief 0〜1の値をラベル側にパーセント表記で併記する属性
 * =====================================*/
using UnityEngine;

namespace CommandBattleCore
{
    /// <summary>
    /// 0〜1 で保持する確率・割合の値を、ラベルにパーセント表記を併記して表示する属性。
    /// <para>
    /// 値そのものは 0〜1 のまま扱う。計算のたびに 1/100 する必要がなくなり、
    /// スケールの取り違えも起きない。読みやすさはラベル側で補う。
    /// </para>
    /// <para>
    /// 表示例（<c>[PercentLabel("追加攻撃確率")]</c> で値が 0.5 の場合）:
    /// <code>追加攻撃確率 50%    [====|====] 0.5</code>
    /// </para>
    /// <para>
    /// 「未設定」を負値の番兵で表す場合は <paramref name="aNegativeText"/> を指定すると、
    /// 値が負のときパーセントの代わりにその文字列を出す。
    /// </para>
    /// </summary>
    public class PercentLabelAttribute : PropertyAttribute
    {
        /// <summary>インスペクタに表示する名前。パーセント表記はこの後ろに付く。</summary>
        public string Text { get; }
        /// <summary>スライダーの下限。</summary>
        public float Min { get; }
        /// <summary>スライダーの上限。</summary>
        public float Max { get; }
        /// <summary>
        /// 値が負のときにパーセントの代わりに表示する文字列。
        /// null なら常にパーセント表記になる。
        /// </summary>
        public string NegativeText { get; }

        /// <param name="aText">表示名。</param>
        /// <param name="aMin">スライダーの下限。既定は 0。</param>
        /// <param name="aMax">スライダーの上限。既定は 1。</param>
        /// <param name="aNegativeText">負値のときに表示する文字列。番兵値を使う場合に指定する。</param>
        public PercentLabelAttribute(string aText, float aMin = 0f, float aMax = 1f, string aNegativeText = null)
        {
            Text = aText;
            Min = aMin;
            Max = aMax;
            NegativeText = aNegativeText;
        }
    }
}
