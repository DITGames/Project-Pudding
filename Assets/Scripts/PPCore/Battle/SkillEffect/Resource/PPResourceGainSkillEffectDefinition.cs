/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPResourceGainSkillEffectDefinition.cs
 * @author hqrse
 * @date 2026/08/06
 * @brief ゲージ回復型スキルエフェクトの定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 対象ユニットのゲージを回復するスキルエフェクト
    // ApplyTarget = 発動者 の場合は aTarget が発動者自身になるため、発動者のゲージが回復する
    // ゲージはユニット専有のため、対象 1 体ごとにそのユニットのゲージへ直接加算する
    [Serializable]
    [PPTypeMenuName("ゲージ回復")]
    public class PPResourceGainSkillEffectDefinition : PPSkillEffectDefinition
    {
        [Label("対象ゲージ")]
        [SerializeField] private PPGaugeKind mKind = PPGaugeKind.Skill;
        [Label("回復量")]
        [SerializeField] private float mAmount = 0f;

        // aSource : スキル発動者
        // aTarget : ゲージを回復する対象
        // aSourceSkill : この効果を保有するスキル定義
        // aContext : バトルコンテキスト
        public override void Apply(BattleUnit aSource, BattleUnit aTarget, PPSkillDefinition aSourceSkill, BattleContext aContext)
        {
            if (aTarget is not PPBattleUnit ppTarget) return;

            ppTarget.ExtraParameters.Gauge(mKind).Recover(mAmount);
        }

        public override string BuildString()
            => $"ゲージ回復：{PPGaugeUtility.ToDisplayString(mKind)} {mAmount}";
    }
}
