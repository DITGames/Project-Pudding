/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPEffectCureSkillDefinition.cs
 * @author hqrse
 * @date 2026/07/28
 * @brief 指定カテゴリのPPEffectの解除系スキル定義
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    [CreateAssetMenu(fileName = "PPEffectCureSkillDefinition",
        menuName = "Project-Pudding/Skill/PPEffectCureSkillDefinition")]
    public class PPEffectCureSkillDefinition : PPSkillDefinition
    {
        [Header("状態異常解除")]
        [Label("解除するカテゴリ")]
        [SerializeField]protected PPStatusEffectCategory mStatusCureMask = PPStatusEffectCategory.None;
        [Header("パラメータ変動解除")]
        [Label("解除するカテゴリ")]
        [SerializeField]protected PPParameterEffectCategory mParameterCureMask = PPParameterEffectCategory.None;

        protected override Action<BattleUnit, List<BattleUnit>, BattleContext> BuildEffect()
        {
            return (src, targets, ctx) =>
            {
                foreach (var tgt in targets)
                {
                    var statusMatched = tgt.ActiveStatusEffects
                        .OfType<PPStatusEffect>()
                        .Where(e => (e.Category & mStatusCureMask) != 0)
                        .ToList();

                    foreach (var eff in statusMatched)
                    {
                        tgt.RemoveStatusEffect(eff);
                    }
                    
                    var parameterMatched = tgt.ActiveStatusEffects
                        .OfType<PPParameterEffect>()
                        .Where(e => (e.Category & mParameterCureMask) != 0)
                        .ToList();

                    foreach (var eff in parameterMatched)
                    {
                        tgt.RemoveStatusEffect(eff);
                    }
                }
            };
        }
    }
}