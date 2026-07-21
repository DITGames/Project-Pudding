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
    [CreateAssetMenu(fileName = "PPResourceRatioCondition", menuName = "Project-Pudding/AI/Conditions/リソース割合")]
    public sealed class PPResourceRatioCondition : PPPartyConditionValidator
    {
        [Label("対象リソース")] public PPResourceType ResourceType = PPResourceType.Normal;
        [Label("比較")] public PPCompareOp Op = PPCompareOp.GreaterOrEqual;
        [Label("割合")] [Range(0f, 100f)] public float Threshold = 100;
        [Label("許容値")] [EditCondition("IsEqualOp", true, false)] public float Tolerance = 1f;
        
        private bool IsEqualOp
            => Op == PPCompareOp.Equal || Op == PPCompareOp.NotEqual;

        public override bool Evaluate(PPPartyAIContext aSnapShot)
        {
            float max = aSnapShot.ResourcePool.Max(ResourceType);
            float ratio = max > 0f ? aSnapShot.Current(ResourceType) / max : 0f;
            return PPConditionMath.Compare(ratio, Op, Threshold, Tolerance);
        }
        
        [ContextMenu("説明文を生成")]
        protected override void BuildString()
        {
            var resource = GetResourceTypeString(ResourceType) + $"リソースが{Threshold}%";
            var op = GetOpString(Op);
            mDescription = resource + op;

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