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
    // 指定した種別のエフェクトを解除するスキルの定義
    // 状態異常・パラメータ変動を問わず PPEffectCategory のビットマスク1本で指定できるため、
    // 系統をまたいだ解除(例: 毒+攻撃力デバフを同時に解除)も1つの定義で表現できる
    // Unremovable タグが付いたエフェクトはマスクが一致しても解除対象から除外する
    [CreateAssetMenu(fileName = "PPEffectCureSkillDefinition",
        menuName = "Project-Pudding/Skill/PPEffectCureSkillDefinition")]
    public class PPEffectCureSkillDefinition : PPSkillDefinition
    {
        [Header("エフェクト解除")]
        [Label("解除するカテゴリ")]
        [SerializeField]protected PPEffectCategory mCureMask = PPEffectCategory.AllAilment;

        // 対象のエフェクト一覧からマスクに一致するものを解除する効果を組み立てる
        // 除去中に BattleUnit.ActiveStatusEffects が変化するため、
        // 一度 ToList() で確定させてから removal を回している
        // return : 効果本体のデリゲート
        protected override Action<BattleUnit, List<BattleUnit>, BattleContext> BuildEffect()
        {
            return (src, targets, ctx) =>
            {
                long mask = (long)mCureMask;
                foreach (var tgt in targets)
                {
                    var matched = tgt.ActiveStatusEffects
                        .Where(e => (e.Category & mask) != 0
                                 && (e.Tags & StatusEffectTag.Unremovable) == 0)
                        .ToList();

                    foreach (var effect in matched)
                    {
                        tgt.RemoveStatusEffect(effect, ctx);
                    }
                }
            };
        }
    }
}
