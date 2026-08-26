/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitGaugeAmountCondition.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニット条件 : ゲージ残量(絶対値)
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ユニット条件: そのユニットのゲージ残量を絶対値で判定する
    // 「特定のスキルを撃つのに必要な量が溜まったか」のように、
    // 具体的なコスト量と突き合わせたい場合はこちらを使う（割合で見るなら PPUnitGaugeRatioCondition）
    [Serializable]
    [PPTypeMenuName("ゲージ/残量(絶対値)")]
    public sealed class PPUnitGaugeAmountCondition : PPUnitConditionValidator
    {
        [Label("対象ゲージ")]
        [SerializeField] private PPGaugeKind mKind = PPGaugeKind.Skill;
        [Label("比較")]
        [SerializeField] private PPCompareOp mOp = PPCompareOp.GreaterOrEqual;
        [Label("ゲージ量")]
        [SerializeField] private float mThreshold = 20f;
        // 等値判定の許容誤差。等値・非等値のときのみ表示される
        [Label("許容値")]
        [EditCondition("IsEqualOp", true, false)]
        [SerializeField] private float mTolerance = 1f;

        // 許容値の入力欄を出すかどうか（等値系の演算子でのみ意味を持つ）
        private bool IsEqualOp
            => mOp == PPCompareOp.Equal || mOp == PPCompareOp.NotEqual;

        // ゲージの現在値を閾値と比較する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
            => aUnit != null
            && PPConditionMath.Compare(aUnit.ExtraParameters.Gauge(mKind).Current, mOp, mThreshold, mTolerance);

        // 設定内容から説明文を組み立てる。等値系のときは許容値も併記する
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            mDescription = $"{PPGaugeUtility.ToDisplayString(mKind)}が{mThreshold}{GetOpString(mOp)}";

            if (IsEqualOp)
            {
                mDescription += $" 許容値({mTolerance})";
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
