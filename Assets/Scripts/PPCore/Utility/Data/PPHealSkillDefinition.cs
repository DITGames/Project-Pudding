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
    /// <summary>
    /// リソース消費に対応した回復スキルの定義。
    /// 回復量はスキルパワーそのままで、属性相性の影響を受けない。
    /// 効果自体は基底の回復スキルと同じだが、<see cref="PPSkillDefinition"/> を継承することで
    /// コスト・属性・AI 用ロールを持てる点が異なる。
    /// </summary>
    [CreateAssetMenu(fileName = "PPHealSkillDefinition", menuName = "Project-Pudding/Skill/PPHealSkillDefinition")]
    public class PPHealSkillDefinition : PPSkillDefinition
    {
        /// <summary>
        /// 対象全員をスキルパワー分だけ回復する効果を組み立てる。
        /// 戦闘不能のユニットは <see cref="BattleUnit.ApplyHeal"/> 側で弾かれるため蘇生にはならない。
        /// </summary>
        /// <returns>効果本体のデリゲート。</returns>
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
