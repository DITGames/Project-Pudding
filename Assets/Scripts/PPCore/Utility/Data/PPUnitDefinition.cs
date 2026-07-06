/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitDefinition.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニット定義
 * =====================================*/
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    [CreateAssetMenu(fileName = "PPBattleUnitDefinition", menuName = "Project-Pudding/Definition/PPUnitDefinition")]
    public class PPUnitDefinition : UnitDefinition
    {
        [Header("PPユニット")]
        [Label("ステータス")] // スキル前提なら消す
        [SerializeField] protected PPStatBlock mPpStatBlock;
        
        public PPStatBlock PpStatBlock => mPpStatBlock; 

        public override BattleUnit CreateRuntimeUnit(ICommandDecider aDecider = null)
        {
            var unit = new PPBattleUnit(mUnitId, DisplayName, CreateParameterSet(), CreatePPParameterSet())
            {
                CommandDecider = aDecider ?? new PPRandomAICommandDecider(),
                SourceDefinition = this,
            };

            foreach (var skill in mSkills)
            {
                if (skill != null)
                {
                    unit.Skills.Add(skill.CreateRuntimeSkill());
                }
            }
            
            return unit;
        }

        protected virtual PPParameterSet CreatePPParameterSet()
            => new(mPpStatBlock.AttackCost);
    }
}