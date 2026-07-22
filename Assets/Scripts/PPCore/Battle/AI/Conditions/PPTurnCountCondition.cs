/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTurnCountCondition.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief パーティ状況条件 : 経過ターン数
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    [PPConditionMenu("進行/経過ターン数", "Progress/TurnCount")]
    [CreateAssetMenu(fileName = "PPTurnCountCondition",
        menuName = "Project-Pudding/AI/Conditions/経過ターン数")]
    public sealed class PPTurnCountCondition : PPPartyConditionValidator
    {
        [Label("比較")] public PPCompareOp Op = PPCompareOp.Equal;
        [Label("ターン数")] public int Threshold = 3;

        public override bool Evaluate(PPPartyAIContext aSnapShot)
         => PPConditionMath.Compare(aSnapShot.Context.TurnCount, Op, Threshold);

        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            var prefix = $"経過ターン数が{Threshold}ターン";
            var op = GetOpString(Op);
            mDescription = prefix + op;
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