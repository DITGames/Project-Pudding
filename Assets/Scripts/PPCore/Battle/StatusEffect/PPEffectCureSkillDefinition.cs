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
    /// <summary>
    /// 指定した種別のエフェクトを解除するスキルの定義。
    /// <para>
    /// 状態異常とパラメータ変動でマスクを分けて持つため、
    /// 「毒だけ治す」「デバフだけ打ち消す」「その両方」を 1 つの定義で表現できる。
    /// 種別はビットフラグなので複数指定も可能。
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "PPEffectCureSkillDefinition",
        menuName = "Project-Pudding/Skill/PPEffectCureSkillDefinition")]
    public class PPEffectCureSkillDefinition : PPSkillDefinition
    {
        /// <summary>解除する状態異常の種別マスク。</summary>
        [Header("状態異常解除")]
        [Label("解除するカテゴリ")]
        [SerializeField]protected PPStatusEffectCategory mStatusCureMask = PPStatusEffectCategory.None;
        /// <summary>解除するパラメータ変動の種別マスク。</summary>
        [Header("パラメータ変動解除")]
        [Label("解除するカテゴリ")]
        [SerializeField]protected PPParameterEffectCategory mParameterCureMask = PPParameterEffectCategory.None;

        /// <summary>
        /// 対象のエフェクト一覧からマスクに一致するものを解除する効果を組み立てる。
        /// <para>
        /// 除去中に <see cref="BattleUnit.ActiveStatusEffects"/> が変化するため、
        /// 一度 ToList() で確定させてから removal を回している。
        /// </para>
        /// </summary>
        /// <returns>効果本体のデリゲート。</returns>
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
