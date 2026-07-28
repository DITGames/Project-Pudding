/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPParameterEffectDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief パラメータ異常のデータ定義
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    public enum PPModifierTargetParam
    {
        [InspectorName(PPParameterEffectCategoryDefinition.NameAttack)]
        Attack,
        [InspectorName(PPParameterEffectCategoryDefinition.NameDefense)]
        Defense,
        [InspectorName(PPParameterEffectCategoryDefinition.NameSpeed)]
        Speed,
        [InspectorName(PPParameterEffectCategoryDefinition.NameHp)]
        Hp,
        [InspectorName(PPParameterEffectCategoryDefinition.NameCost)]
        Cost,
    }
    
    public enum PPModifierDirection
    {
        [InspectorName("バフ")]
        Increase,
        [InspectorName("デバフ")]
        Decrease,
    }
    
    [CreateAssetMenu(fileName = "PPParameterEffectDefinition", menuName = "Project-Pudding/Effect/PPParameterEffectDefinition")]
    public class PPParameterEffectDefinition : PPEffectDefinition
    {
        [Header("パラメータエフェクト")]
        [Label("対象パラメータ")]
        [SerializeField]protected PPModifierTargetParam mTargetParam = PPModifierTargetParam.Attack;
        [Label("種別")]
        [SerializeField]protected PPModifierDirection mDirection = PPModifierDirection.Increase;
        [Label("変動タイプ")]
        [SerializeField]protected ParameterModifierType mModifierType = ParameterModifierType.Add;
        [Label("変動量")][Min(0)]
        [SerializeField]protected float mValue = 10f;
        
        public PPModifierTargetParam TargetParam => mTargetParam;
        public PPModifierDirection Direction => mDirection;
        public ParameterModifierType ModifierType => mModifierType;
        public float Value => mValue;
        
        private PPParameterEffectCategory ResolveCategory()
            => (mTargetParam, mDirection) switch
            {
                (PPModifierTargetParam.Attack, PPModifierDirection.Increase) => PPParameterEffectCategory.AttackBuff,
                (PPModifierTargetParam.Attack, PPModifierDirection.Decrease) => PPParameterEffectCategory.AttackDebuff,
                (PPModifierTargetParam.Defense, PPModifierDirection.Increase) => PPParameterEffectCategory.DefenseBuff,
                (PPModifierTargetParam.Defense, PPModifierDirection.Decrease) => PPParameterEffectCategory.DefenseDebuff,
                (PPModifierTargetParam.Speed, PPModifierDirection.Increase) => PPParameterEffectCategory.SpeedBuff,
                (PPModifierTargetParam.Speed, PPModifierDirection.Decrease) => PPParameterEffectCategory.SpeedDebuff,
                (PPModifierTargetParam.Hp, PPModifierDirection.Increase) => PPParameterEffectCategory.HpBuff,
                (PPModifierTargetParam.Hp, PPModifierDirection.Decrease) => PPParameterEffectCategory.HpDebuff,
                (PPModifierTargetParam.Cost, PPModifierDirection.Increase) => PPParameterEffectCategory.CostBuff,
                (PPModifierTargetParam.Cost, PPModifierDirection.Decrease) => PPParameterEffectCategory.CostDebuff,
                _ => PPParameterEffectCategory.None,
            };
        
        private float ResolveModifier()
            => (mModifierType, mDirection) switch
            {
                (ParameterModifierType.Add, PPModifierDirection.Increase) => mValue,
                (ParameterModifierType.Add, PPModifierDirection.Decrease) => -mValue,
                (ParameterModifierType.Multiply, PPModifierDirection.Increase) when mValue < 1 => 1f,
                (ParameterModifierType.Multiply, PPModifierDirection.Increase) => mValue,
                (ParameterModifierType.Multiply, PPModifierDirection.Decrease) when mValue > 1 => 1f,
                (ParameterModifierType.Multiply, PPModifierDirection.Decrease) => mValue,
                (_,_) => mValue,
            };

        public override StatusEffect CreateRuntimeStatusEffect(BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            var effect = new PPParameterEffect(mEffectId, mDisplayName, new TurnDurationCondition(mDuration))
            {
                StackPolicy = mStackPolicy,
                MaxStacks = mMaxStack,
                Category = ResolveCategory(),
            };
            ConfigureEffect(effect, aSource, aTarget, aContext);
            return effect;
        }
        
        protected string ParamId
            => mTargetParam switch
            {
                PPModifierTargetParam.Attack => ParameterSet.ParamIdAttack,
                PPModifierTargetParam.Defense => ParameterSet.ParamIdDefense,
                PPModifierTargetParam.Speed => ParameterSet.ParamIdSpeed,
                PPModifierTargetParam.Hp => ParameterSet.ParamIdMaxHp,
                PPModifierTargetParam.Cost => PPParameterSet.ParameterIdAttackCost,
                _ => string.Empty,
            };

        protected override void ConfigureEffect(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            if (aEffect is PPParameterEffect effect)
            {
                aEffect.AddModifier(ParamId, mModifierType, ResolveModifier());
            }
        }

        protected override string BuildAutoEffectId()
            => $"Param_{mTargetParam}_{mDirection}_{mModifierType}_{mValue}_{mDuration}";
    }
}