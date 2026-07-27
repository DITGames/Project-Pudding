/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPHealSkillDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief PPCore固有の回復スキル定義
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    [CreateAssetMenu(fileName = "PPHealSkillDefinition", menuName = "Project-Pudding/Skill/PPHealSkillDefinition")]
    public class PPHealSkillDefinition : PPSkillDefinition
    {
        protected override Action<BattleUnit, List<BattleUnit>, BattleContext> BuildEffect()
        {
            return (src, targets, ctx) =>
            {
                foreach (var tgt in targets)
                {
                    tgt.ApplyHeal(mPower);
                }
            };
        }
    }
}