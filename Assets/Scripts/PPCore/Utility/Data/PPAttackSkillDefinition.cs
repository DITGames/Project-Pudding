/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPAttackSkillDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief PPCore固有の攻撃スキル定義
 * =====================================*/
using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    [CreateAssetMenu(fileName = "PPAttackSkillDefinition", menuName = "Project-Pudding/Skill/PPAttackSkillDefinition")]
    public class PPAttackSkillDefinition : PPSkillDefinition
    {
        protected override Action<BattleUnit, List<BattleUnit>, BattleContext> BuildEffect()
        {
            return (src, targets, ctx) =>
            {
                var attribute = PPDamageUtility.ResolveAttribute(mAttribute, src);
                foreach (var tgt in targets)
                {
                    float dmg = PPDamageUtility.ResolveAttackSkillDamage(src, tgt, this);
                    var damageInfo = PPDamageUtility.CreateDamageInfo(src, tgt, dmg, mCategory, attribute, this, ctx);
                    tgt.ApplyDamage(damageInfo);
                }
            };
        }
    }
}