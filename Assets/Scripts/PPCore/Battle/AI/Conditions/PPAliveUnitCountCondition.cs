/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPAllyAliveCountCondition.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief パーティ状況条件 : 味方ユニット生存数
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    public enum PPAliveUnitCountConditionType
    {
        [InspectorName("味方")]
        Ally,
        [InspectorName("敵")]
        Enemy,
        [InspectorName("全体")]
        All,
    }
    
    [CreateAssetMenu(fileName = "PPAllyAliveCountCondition", menuName = "Project-Pudding/AI/Conditions/ユニット生存数")]
    public sealed class PPAliveUnitCountCondition : PPPartyConditionValidator
    {
        [Label("対象")] public PPAliveUnitCountConditionType ConditionType = PPAliveUnitCountConditionType.Ally; 
        [Label("比較")] public PPCompareOp Op = PPCompareOp.GreaterOrEqual;
        [Label("ユニット数")] public int Threshold = 2;

        public override bool Evaluate(PPPartyAIContext aSnapShot)
        => ConditionType switch
        {
            PPAliveUnitCountConditionType.Ally => PPConditionMath.Compare(aSnapShot.AliveMembers.Count, Op, Threshold),
            PPAliveUnitCountConditionType.Enemy => PPConditionMath.Compare(aSnapShot.AliveEnemies.Count, Op, Threshold),
            PPAliveUnitCountConditionType.All => PPConditionMath.Compare(aSnapShot.AliveMembers.Count + aSnapShot.AliveEnemies.Count, Op, Threshold),
            _ => false
        };
        
        [ContextMenu("説明文を生成")]
        protected override void BuildString()
        {
            var tgt = GetTargetString();
            var op = GetOpString(Op);
            var num = Threshold + "体";
            mDescription = tgt + num + op;
        }
        
        private string GetTargetString()
            => ConditionType switch
            {
                PPAliveUnitCountConditionType.Ally => "味方の生存ユニット数が",
                PPAliveUnitCountConditionType.Enemy => "敵の生存ユニット数が",
                PPAliveUnitCountConditionType.All => "全体の生存ユニット数が",
                _ => ""
            };

        protected override string GetOpString(PPCompareOp aOp)
            => aOp switch
            {
                PPCompareOp.Equal => "",
                PPCompareOp.NotEqual => "ではない",
                _ => base.GetOpString(aOp),
            };
    }
}