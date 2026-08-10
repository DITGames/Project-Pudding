/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPHealSkillEffectDefinition.cs
 * @author hqrse
 * @date 2026/08/06
 * @brief 回復型スキルエフェクトの定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 対象を回復量分だけ回復するスキルエフェクト
    // 戦闘不能のユニットは BattleUnit.ApplyHeal 側で弾かれるため蘇生にはならない
    [Serializable]
    [PPTypeMenuName("回復")]
    public class PPHealSkillEffectDefinition : PPSkillEffectDefinition
    {
        [Label("回復量")]
        [SerializeField] private float mPower = 10f;

        // aSource : スキル発動者
        // aTarget : 回復する対象
        // aSourceSkill : この効果を保有するスキル定義
        // aContext : バトルコンテキスト
        public override void Apply(BattleUnit aSource, BattleUnit aTarget, PPSkillDefinition aSourceSkill, BattleContext aContext)
        {
            aTarget.ApplyHeal(mPower);
        }

        // 回復量をそのまま見積もりとして返す
        // 対象の HP がどれだけ欠けているかは AI 側（PPActionUtilityEvaluator）で考慮する
        // aSource : スキル発動者
        // aTarget : 回復する対象
        // aContext : バトルコンテキスト
        // return : 回復量の見積もり
        public override PPEffectEstimate Estimate(BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
            => PPEffectEstimate.FromHeal(mPower);

        public override string BuildString()
            => $"回復：{mPower}";
    }
}
