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
    [PPConditionMenu("パーティ状態/HP割合", "Party/HpRatio")]
    [CreateAssetMenu(fileName = "PPPartyHpRatioCondition",
        menuName = "Project-Pudding/AI/Conditions/パーティHP割合")]
    public sealed class PPPartyHpRatioCondition : PPPartyConditionValidator
    {
        [Label("比較")] public PPCompareOp Op = PPCompareOp.Equal;
        [Label("割合")][Range(0f, 100f)] public float Threshold = 0f;
        [Label("許容値")][EditCondition("IsEqualOp", true, false)] public float Tolerance = 0f;
        
        bool IsEqualOp
        => Op == PPCompareOp.Equal
        || Op == PPCompareOp.NotEqual;

        public override bool Evaluate(PPPartyAIContext aSnapShot)
        => PPConditionMath.Compare(aSnapShot.PartyHpRatio, Op, Threshold, Tolerance);

        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            var prefix = "HPが";
            var ratio = Threshold + "%";
            var op = GetOpString(Op);
            mDescription = prefix + ratio + op;

            if (Op == PPCompareOp.Equal || Op == PPCompareOp.NotEqual)
            {
                mDescription += $" 許容値({Tolerance}%)";
            }
        }
        
        protected override string GetOpString(PPCompareOp aOp)
            => aOp switch
            {
                PPCompareOp.Equal => "と等しい",
                PPCompareOp.NotEqual => "と等しくない",
                _ => base.GetOpString(aOp)
            };
    }
}