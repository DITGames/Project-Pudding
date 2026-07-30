/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyHpRatioCondition.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief パーティ状況条件 : パーティHP割合
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// パーティ状況条件: パーティ全体の HP 割合。
    /// 個々の残 HP ではなく合計 HP に対する割合で見るため、
    /// 「全体的に消耗している」状況の判定に向く。閾値は 0～1 で保持する。
    /// </summary>
    [PPConditionMenu("パーティ状態/HP割合", "Party/HpRatio")]
    [CreateAssetMenu(fileName = "PPPartyHpRatioCondition",
        menuName = "Project-Pudding/AI/Conditions/パーティHP割合")]
    public sealed class PPPartyHpRatioCondition : PPPartyConditionValidator
    {
        /// <summary>比較演算子。</summary>
        [Label("比較")] public PPCompareOp Op = PPCompareOp.Equal;
        /// <summary>閾値（0〜1）。</summary>
        [PercentLabel("割合")] public float Threshold = 0f;
        /// <summary>等値判定の許容誤差（0〜1）。等値・非等値のときのみ表示される。</summary>
        [Label("許容値")][EditCondition("IsEqualOp", true, false)] public float Tolerance = 0f;

        /// <summary>許容値の入力欄を出すかどうか（等値系の演算子でのみ意味を持つ）。</summary>
        bool IsEqualOp
        => Op == PPCompareOp.Equal
        || Op == PPCompareOp.NotEqual;

        /// <summary>
        /// パーティ全体の HP 割合を閾値と比較する。
        /// 双方とも 0〜1 の尺度のためそのまま渡す。
        /// </summary>
        /// <param name="aSnapShot">評価対象のパーティ状況スナップショット。</param>
        /// <returns>条件を満たす場合 true。</returns>
        public override bool Evaluate(PPPartyAIContext aSnapShot)
        => PPConditionMath.Compare(aSnapShot.PartyHpRatio, Op, Threshold, Tolerance);

        /// <summary>
        /// 設定内容から説明文を組み立てる。等値系のときは許容値も併記する。
        /// 値は 0〜1 で保持しているが、説明文は読みやすさを優先してパーセント表記にする。
        /// </summary>
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            var prefix = "HPが";
            var ratio = $"{Threshold * 100f:0.#}%";
            var op = GetOpString(Op);
            mDescription = prefix + ratio + op;

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
