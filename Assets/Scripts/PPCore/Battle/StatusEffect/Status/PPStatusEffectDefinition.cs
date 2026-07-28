/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPStatusEffectDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief ステータス異常のデータ定義
 * =====================================*/
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    public abstract class PPStatusEffectDefinition : PPEffectDefinition
    {
        [Header("ステータスエフェクト")]
        [Label("カテゴリ")]
        [SerializeField]protected PPStatusEffectCategory mCategory = PPStatusEffectCategory.None;
        
        public PPStatusEffectCategory Category => mCategory;

        public override StatusEffect CreateRuntimeStatusEffect(BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            var effect = new PPStatusEffect(mEffectId, mDisplayName, new TurnDurationCondition(mDuration))
            {
                StackPolicy = mStackPolicy,
                MaxStacks = mMaxStack,
                Category = mCategory,
            };
            ConfigureEffect(effect, aSource, aTarget, aContext);
            return effect;
        }
    }
}