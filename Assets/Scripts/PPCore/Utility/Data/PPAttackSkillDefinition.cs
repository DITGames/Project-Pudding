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
    /// <summary>
    /// 属性相性を考慮する攻撃スキルの定義。
    /// <para>
    /// 基底の <see cref="AttackSkillDefinition"/> と違い、ダメージ計算を
    /// <see cref="PPDamageUtility"/> に委ねることで、命中・クリティカルに加えて
    /// 属性相性による倍率まで一貫して適用される。
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "PPAttackSkillDefinition", menuName = "Project-Pudding/Skill/PPAttackSkillDefinition")]
    public class PPAttackSkillDefinition : PPSkillDefinition
    {
        /// <summary>
        /// 対象全員にダメージを与える効果を組み立てる。
        /// 属性は対象ごとではなく最初に 1 度だけ解決する
        /// （無属性スキルは使用者の属性を継承するため、対象によって変わらない）。
        /// </summary>
        /// <returns>効果本体のデリゲート。</returns>
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
