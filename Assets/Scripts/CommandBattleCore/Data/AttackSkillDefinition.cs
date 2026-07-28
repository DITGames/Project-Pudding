/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AttackSkillDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief 攻撃系スキルのベース定義
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CommandBattleCore
{
    public enum AttackCategory
    {
        Physical,
        Magic
    }
    [CreateAssetMenu(fileName = "AttackSkillDefinition", menuName = "CommandBattleCore/AttackSkillDefinition")]
    public class AttackSkillDefinition : SkillDefinition
    {
        [Label("種別")]
        [SerializeField] protected AttackCategory mCategory;
        public AttackCategory Category => mCategory;

        protected override Action<BattleUnit, List<BattleUnit>, BattleContext> BuildEffect()
        {
            return (src, targets, ctx) =>
            {
                foreach (var t in targets)
                {
                    float dmg = Math.Max(1f, src.Parameters.Attack.CurrentValue + mPower
                                             - t.Parameters.Defense.CurrentValue * 0.5f);

                    var damageInfo = new DamageInfo(src, t, dmg, this);
                    var hit = ctx.ResolveHit(src, t, damageInfo);

                    if (hit.mResult == HitResult.Miss)
                    {
                        damageInfo.IsMiss = true;
                        damageInfo.Amount = 0f;
                    }

                    if (hit.mCriticalInfo.IsCritical)
                    {
                        damageInfo.IsCritical = true;
                        damageInfo.Amount *= hit.mCriticalInfo.CriticalMultiplier;
                    }

                    t.ApplyDamage(damageInfo);
                }
            };
        }
    }
}