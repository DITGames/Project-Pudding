/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PercentLabelAttribute.cs
 * @author hqrse
 * @date 2026/07/30
 * @brief 0〜1の値をラベル側にパーセント表記で併記する属性
 * =====================================*/
using UnityEngine;

namespace AttributeUtility
{
    // 0〜1 で保持する確率・割合の値を、ラベルにパーセント表記を併記して表示する属性
    // 値そのものは 0〜1 のまま扱う。計算のたびに 1/100 する必要がなくなり、スケールの取り違えも起きない。読みやすさはラベル側で補う
    // 表示例（[PercentLabel("追加攻撃確率")] で値が 0.5 の場合）: 追加攻撃確率 50%    [====|====] 0.5
    // 「未設定」を 0 の番兵で表す場合は aZeroText を指定すると、値が 0 のときパーセントの代わりにその文字列を出す
    public class PercentLabelAttribute : PropertyAttribute
    {
        // インスペクタに表示する名前。パーセント表記はこの後ろに付く
        public string Text { get; }
        // スライダーの下限
        public float Min { get; }
        // スライダーの上限
        public float Max { get; }
        // 値が 0 のときにパーセントの代わりに表示する文字列。null なら 0 も「0%」として表示する
        public string ZeroText { get; }

        // aText : 表示名
        // aMin : スライダーの下限。既定は 0
        // aMax : スライダーの上限。既定は 1
        // aZeroText : 値が 0 のときに表示する文字列。0 を番兵値として使う場合に指定する
        public PercentLabelAttribute(string aText, float aMin = 0f, float aMax = 1f, string aZeroText = null)
        {
            Text = aText;
            Min = aMin;
            Max = aMax;
            ZeroText = aZeroText;
        }
    }
}
