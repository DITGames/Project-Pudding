/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPoisonEffectDefinition.cs
 * @author hqrse
 * @date 2026/07/28
 * @brief 毎ターンダメージを与える毒のStatusEffect定義
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    [CreateAssetMenu(fileName = "PPPoisonEffectDefinition",
        menuName = "Project-Pudding/Effect/PPPoisonEffectDefinition")]
    public class PPPoisonEffectDefinition : PPStatusEffectDefinition
    {
        [Header("毒")]
        [Label("ダメージ量")]
        [SerializeField]protected float mDamagePerTurn = 5f;

        protected override void ConfigureEffect(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.OnTick = (unit, ctx) =>
            {
                var damageInfo = new PPDamageInfo(aSource, unit, mDamagePerTurn, PPSkillCategory.Debuff, PPTypeAttribute.Normal, this);
                unit.ApplyDamage(damageInfo);
            };
        }

        protected override string BuildAutoEffectId()
            => $"Poison_{mDamagePerTurn}_{mDuration}";
    }
}