/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitRemainingActionCondition.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief ユニット条件 : このティックの残り行動回数
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ユニット条件: このティックで残っている行動回数
    //
    // 行動回数が 2 以上のユニットで「最後の 1 手は必ず攻撃で締める」のような書き分けに使う
    //
    // 残り回数は仮押さえ台帳を差し引いた値で見る
    // 同じティックで既に積んだ行動を数えないと、2 手目の判断が 1 手目と同じ値を見てしまい書き分けができない
    // 台帳が差し込まれていない場合は、行動回数の上限をそのまま残り回数として扱う
    [Serializable]
    [PPTypeMenuName("行動/残り行動回数")]
    public sealed class PPUnitRemainingActionCondition : PPUnitConditionValidator
    {
        [Label("比較")]
        [SerializeField] private PPCompareOp mOp = PPCompareOp.GreaterOrEqual;
        [Label("行動回数")]
        [SerializeField] private int mThreshold = 1;

        // 残り行動回数を閾値と比較する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            int total = aUnit.ResolveActionCount();
            int reserved = aSnapShot.Ledger?.ReservedCount(aUnit) ?? 0;
            return PPConditionMath.Compare(Mathf.Max(0, total - reserved), mOp, mThreshold);
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
            => mDescription = $"残り行動回数が{mThreshold}回{GetOpString(mOp)}";
    }
}
