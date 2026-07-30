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
    // 属性相性を考慮する攻撃スキルの定義
    // 基底の AttackSkillDefinition と違い、ダメージ計算を PPDamageUtility に委ねることで、
    // 命中・クリティカルに加えて属性相性による倍率まで一貫して適用される
    [CreateAssetMenu(fileName = "PPAttackSkillDefinition", menuName = "Project-Pudding/Skill/PPAttackSkillDefinition")]
    public class PPAttackSkillDefinition : PPSkillDefinition
    {
        // 対象全員にダメージを与える効果を組み立てる
        // 属性は対象ごとではなく最初に 1 度だけ解決する
        // （無属性スキルは使用者の属性を継承するため、対象によって変わらない）
        // return : 効果本体のデリゲート
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
