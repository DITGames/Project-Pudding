/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file UnitDefinition.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ユニットのマスターデータ
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;

namespace CommandBattleCore
{
    [CreateAssetMenu(menuName = "CommandBattleCore/UnitDefinition", fileName = "NewUnit")]
    public class UnitDefinition : ScriptableObject
    {
        [Header("ユニット")]
        [Label("ユニットID")]
        [SerializeField] protected string mUnitId;
        [Label("表示名")]
        [SerializeField] protected string mDisplayName;

        [Header("詳細")]
        [Label("ステータス")]
        [SerializeField] protected StatBlock mBaseStatBlock;
        [Label("使用可能スキル", true)]
        [SerializeField] protected List<SkillDefinition> mSkills = new();

        public string UnitId => mUnitId;
        public string DisplayName => mDisplayName;
        public StatBlock BaseStatBlock => mBaseStatBlock;
        public List<SkillDefinition> Skills => mSkills;

        public virtual BattleUnit CreateRuntimeUnit(ICommandDecider aDecider = null)
        {
            var unit = new BattleUnit(mUnitId, mDisplayName, CreateParameterSet())
            {
                CommandDecider = aDecider ?? new RandomAICommandDecider(),
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
        
        protected virtual ParameterSet CreateParameterSet() =>
            new(mBaseStatBlock.MaxHP, mBaseStatBlock.Attack, mBaseStatBlock.Defense, mBaseStatBlock.Speed);
    }
}