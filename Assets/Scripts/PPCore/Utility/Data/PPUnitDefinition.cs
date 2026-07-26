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
        [Header("ユニット拡張")]
        [Label("ステータス")] // スキル前提なら消す
        [SerializeField] protected PPStatBlock mExpandStatBlock;
        
        [Header("パーティAI")]
        [Label("既定ロール")][SerializeField] protected PPUnitRole mDefaultRole = PPUnitRole.Inherit;
        [Label("既定の行動スコア補正")][SerializeField] protected PPUnitActionScoreModifier mDefaultActionScore = new();
        [Label("既定の知能")][SerializeField][Range(-1,100)] protected float mDefaultIntelligence = 50f;
        
        [Header("成長曲線 (X = レベル, Y = 倍率)")]
        [Label("HP成長曲線")][SerializeField] protected AnimationCurve mHpGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label("攻撃力成長曲線")][SerializeField] protected AnimationCurve mAttackGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label("防御力成長曲線")][SerializeField] protected AnimationCurve mDefenseGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label("素早さ成長曲線")][SerializeField] protected AnimationCurve mSpeedGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        
        public PPStatBlock ExpandStatBlock => mExpandStatBlock;
        public PPUnitRole DefaultRole => mDefaultRole;
        public PPUnitActionScoreModifier ActionScoreModifier => mDefaultActionScore;
        public float DefaultIntelligence => mDefaultIntelligence;

        public override BattleUnit CreateRuntimeUnit(ICommandDecider aDecider = null)
            => CreateRuntimeUnit(1, aDecider);

        public virtual BattleUnit CreateRuntimeUnit(int aLevel, ICommandDecider aDecider = null)
        {
            var unit = new PPBattleUnit(mUnitId, DisplayName, CreateParameterSet(aLevel), CreatePPParameterSet())
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

        protected virtual ParameterSet CreateParameterSet(int aLevel)
        {
            var b = mBaseStatBlock;
            return new ParameterSet(
                b.MaxHP * Mathf.Max(1f, mHpGrowth.Evaluate(aLevel)),
                b.Attack * Mathf.Max(1f, mAttackGrowth.Evaluate(aLevel)),
                b.Defense * Mathf.Max(1f, mDefenseGrowth.Evaluate(aLevel)),
                b.Speed * Mathf.Max(1f, mSpeedGrowth.Evaluate(aLevel))
                );
        }

        protected virtual PPParameterSet CreatePPParameterSet()
            => new(mExpandStatBlock.AttackCost);
    }
}