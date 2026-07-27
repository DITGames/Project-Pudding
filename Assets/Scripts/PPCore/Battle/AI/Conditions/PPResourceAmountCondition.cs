/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPResourceAmountCondition.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief パーティ状況条件 : 指定リソース残量(絶対値)
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    [PPConditionMenu("リソース/残量(絶対値)", "Resources/Amount")]
    [CreateAssetMenu(fileName = "PPResourceAmountCondition",
        menuName = "Project-Pudding/AI/Conditions/リソース残量(絶対値)")]
    public sealed class PPResourceAmountCondition : PPPartyConditionValidator
    {
        [Label("対象リソース")] public PPTypeAttribute mTypeAttribute = PPTypeAttribute.Normal;
        [Label("比較")] public PPCompareOp Op = PPCompareOp.GreaterOrEqual;
        [Label("リソース量")] public float Threshold = 20f;
        [Label("許容値")] [EditCondition("IsEqualOp", true, false)]public float Tolerance = 1f;
        
        private bool IsEqualOp
            => Op == PPCompareOp.Equal || Op == PPCompareOp.NotEqual;

        public override bool Evaluate(PPPartyAIContext aSnapShot)
         => PPConditionMath.Compare(aSnapShot.Current(mTypeAttribute), Op, Threshold, Tolerance);

        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            var resource = GetResourceTypeString(mTypeAttribute) + $"リソースが{Threshold}";
            var op = GetOpString(Op);
            mDescription = resource + op;

            if (Op == PPCompareOp.Equal || Op == PPCompareOp.NotEqual)
            {
                mDescription += $" 許容値({Tolerance})";
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