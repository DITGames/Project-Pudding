/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitHpRatioCondition.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief ユニット条件 : HP割合
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // ユニット条件: そのユニットの HP 割合を判定する
    // 「瀕死の味方だけを実行者にする」「まだ余裕のある攻撃役に大技を撃たせる」といった絞り込みに使う
    [Serializable]
    [PPTypeMenuName("ユニット状態/HP割合")]
    public sealed class PPUnitHpRatioCondition : PPUnitConditionValidator
    {
        [Label("比較")]
        [SerializeField] private PPCompareOp mOp = PPCompareOp.LessOrEqual;
        [PercentLabel("HP割合")]
        [SerializeField] private float mThreshold = 0.5f;

        // HP 割合を閾値と比較する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
            => aUnit != null && PPConditionMath.Compare(PPPartyAIContext.HpRatio(aUnit), mOp, mThreshold);

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
            => mDescription = $"HP割合が{mThreshold * 100f:F0}%{GetOpString(mOp)}";
    }
}
