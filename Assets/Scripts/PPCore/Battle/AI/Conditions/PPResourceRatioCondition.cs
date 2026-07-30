/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPResourceRatioCondition.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief パーティ状況条件 : 指定リソースの割合
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// パーティ状況条件: 指定属性のリソースが上限に対して何割溜まっているか。
    /// <para>
    /// 絶対量で見る <see cref="PPResourceAmountCondition"/> と違い、上限に対する充足率で判定する。
    /// 「リソースが満タンに近いので大技を狙う」といった状況判断に使う。
    /// 閾値・許容値ともに 0～1 で扱う。
    /// </para>
    /// </summary>
    [PPConditionMenu("リソース/残量(割合)", "Resources/Ratio")]
    [CreateAssetMenu(fileName = "PPResourceRatioCondition",
        menuName = "Project-Pudding/AI/Conditions/リソース割合")]
    public sealed class PPResourceRatioCondition : PPPartyConditionValidator
    {
        /// <summary>判定対象の属性。</summary>
        [Label("対象リソース")] public PPTypeAttribute mTypeAttribute = PPTypeAttribute.Normal;
        /// <summary>比較演算子。</summary>
        [Label("比較")] public PPCompareOp Op = PPCompareOp.GreaterOrEqual;
        /// <summary>閾値（0〜1）。</summary>
        [PercentLabel("割合")] public float Threshold = 1f;
        /// <summary>等値判定の許容誤差（0〜1）。等値・非等値のときのみ表示される。</summary>
        [Label("許容値")] [EditCondition("IsEqualOp", true, false)] public float Tolerance = 0.01f;

        /// <summary>許容値の入力欄を出すかどうか（等値系の演算子でのみ意味を持つ）。</summary>
        private bool IsEqualOp
            => Op == PPCompareOp.Equal || Op == PPCompareOp.NotEqual;

        /// <summary>
        /// 対象リソースの充足率を求めて閾値と比較する。閾値と同じ 0〜1 の尺度で扱う。
        /// 上限が 0 の場合は 0 として扱う。
        /// </summary>
        /// <param name="aSnapShot">評価対象のパーティ状況スナップショット。</param>
        /// <returns>条件を満たす場合 true。</returns>
        public override bool Evaluate(PPPartyAIContext aSnapShot)
        {
            float max = aSnapShot.ResourcePool.Max(mTypeAttribute);
            float ratio = max > 0f ? aSnapShot.Current(mTypeAttribute) / max : 0f;
            return PPConditionMath.Compare(ratio, Op, Threshold, Tolerance);
        }

        /// <summary>
        /// 設定内容から説明文を組み立てる。等値系のときは許容値も併記する。
        /// 値は 0〜1 で保持しているが、説明文は読みやすさを優先してパーセント表記にする。
        /// </summary>
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            var resource = GetResourceTypeString(mTypeAttribute) + $"リソースが{Threshold * 100f:0.#}%";
            var op = GetOpString(Op);
            mDescription = resource + op;

            if (Op == PPCompareOp.Equal || Op == PPCompareOp.NotEqual)
            {
                mDescription += $" 許容値({Tolerance * 100f:0.#}%)";
            }
        }

        /// <summary>説明文の語尾を自然な日本語にするため、等値系のみ表記を差し替える。</summary>
        /// <param name="aOp">比較演算子。</param>
        protected override string GetOpString(PPCompareOp aOp)
            => aOp switch
            {
                PPCompareOp.Equal => "と等しい",
                PPCompareOp.NotEqual => "と等しくない",
                _ => base.GetOpString(aOp)
            };
    }
}
