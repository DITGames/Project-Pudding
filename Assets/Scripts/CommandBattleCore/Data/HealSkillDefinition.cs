/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file HealSkillDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief 回復系スキルのベース定義
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CommandBattleCore
{
    [CreateAssetMenu(fileName = "HealSkillDefinition", menuName = "CommandBattleCore/HealSkillDefinition")]
    public class HealSkillDefinition : SkillDefinition
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