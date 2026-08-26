/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitGaugeRatioCondition.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニット条件 : ゲージ残量(割合)
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ユニット条件: そのユニットのゲージが上限に対して何割溜まっているか
    // 絶対量で見る PPUnitGaugeAmountCondition と違い、上限に対する充足率で判定する
    // 「ゲージが満タンに近いので大技を狙う」といった状況判断に使う
    // 閾値・許容値ともに 0～1 で扱う
    [Serializable]
    [PPTypeMenuName("ゲージ/残量(割合)")]
    public sealed class PPUnitGaugeRatioCondition : PPUnitConditionValidator
    {
        [Label("対象ゲージ")]
        [SerializeField] private PPGaugeKind mKind = PPGaugeKind.Skill;
        [Label("比較")]
        [SerializeField] private PPCompareOp mOp = PPCompareOp.GreaterOrEqual;
        [PercentLabel("割合")]
        [SerializeField] private float mThreshold = 1f;
        // 等値判定の許容誤差（0〜1）。等値・非等値のときのみ表示される
        [Label("許容値")]
        [EditCondition("IsEqualOp", true, false)]
        [SerializeField] private float mTolerance = 0.01f;

        // 許容値の入力欄を出すかどうか（等値系の演算子でのみ意味を持つ）
        private bool IsEqualOp
            => mOp == PPCompareOp.Equal || mOp == PPCompareOp.NotEqual;

        // ゲージの充足率を閾値と比較する
        // 上限が 0 の場合は ResourceParameter.Ratio が 0 を返すため、0 として扱われる
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
            => aUnit != null
            && PPConditionMath.Compare(aUnit.ExtraParameters.Gauge(mKind).Ratio, mOp, mThreshold, mTolerance);

        // 設定内容から説明文を組み立てる。等値系のときは許容値も併記する
        // 値は 0〜1 で保持しているが、説明文は読みやすさを優先してパーセント表記にする
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            mDescription = $"{PPGaugeUtility.ToDisplayString(mKind)}が{mThreshold * 100f:0.#}%{GetOpString(mOp)}";

            if (IsEqualOp)
            {
                mDescription += $" 許容値({mTolerance * 100f:0.#}%)";
            }
        }

        // 説明文の語尾を自然な日本語にするため、等値系のみ表記を差し替える
        // aOp : 比較演算子
        protected override string GetOpString(PPCompareOp aOp)
            => aOp switch
            {
                PPCompareOp.Equal => "と等しい",
                PPCompareOp.NotEqual => "と等しくない",
                _ => base.GetOpString(aOp)
            };
    }
}
