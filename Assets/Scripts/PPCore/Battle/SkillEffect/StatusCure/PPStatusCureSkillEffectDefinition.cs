/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPStatusCureSkillEffectDefinition.cs
 * @author hqrse
 * @date 2026/08/06
 * @brief StatusEffect解除型スキルエフェクトの定義
 * =====================================*/

using System;
using System.Linq;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 指定した種別のエフェクトを対象から解除するスキルエフェクト
    // 状態異常・パラメータ変動を問わず PPEffectCategory のビットマスク1本で指定できるため、
    // 系統をまたいだ解除(例: 毒+攻撃力デバフを同時に解除)も1つの定義で表現できる
    // Unremovable タグが付いたエフェクトはマスクが一致しても解除対象から除外する
    [Serializable]
    [PPTypeMenuName("StatusEffect解除")]
    public class PPStatusCureSkillEffectDefinition : PPSkillEffectDefinition
    {
        [Label("解除するカテゴリ")]
        [SerializeField] private PPEffectCategory mCureMask = PPEffectCategory.AllAilment;

        // 対象のエフェクト一覧からマスクに一致するものを解除する
        // 除去中に BattleUnit.ActiveStatusEffects が変化するため、
        // 一度 ToList() で確定させてから removal を回している
        // aSource : スキル発動者
        // aTarget : StatusEffect を解除する対象
        // aSourceSkill : この効果を保有するスキル定義
        // aContext : バトルコンテキスト
        public override void Apply(BattleUnit aSource, BattleUnit aTarget, PPSkillDefinition aSourceSkill, BattleContext aContext)
        {
            long mask = (long)mCureMask;
            var matched = aTarget.ActiveStatusEffects
                .Where(e => (e.Category & mask) != 0
                         && (e.Tags & StatusEffectTag.Unremovable) == 0)
                .ToList();

            foreach (var effect in matched)
            {
                aTarget.RemoveStatusEffect(effect, aContext);
            }
        }

        public override string BuildString()
            => $"StatusEffect解除：{mCureMask}";
    }
}
