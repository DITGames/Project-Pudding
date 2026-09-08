/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStatusApplySkillEffectDefinition.cs
 * @author hqrse
 * @date 2026/08/06
 * @brief StatusEffect付与型スキルエフェクトの定義
 * =====================================*/

using System;
using CommandBattleCore;
using CustomConsole;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 参照する PPEffectDefinition アセット（毒・パラメータ変動）から StatusEffect を生成し、対象に付与するスキルエフェクト
    // 効果の値は参照先アセットに一元化されており、このクラス自身は値を持たない
    [Serializable]
    [PPTypeMenuName("StatusEffect付与")]
    public class PPStatusApplySkillEffectDefinition : PPSkillEffectDefinition
    {
        [Label("エフェクト")]
        [SerializeField]
        private PPEffectDefinition mEffect;

        // 付与に成功する確率（0～1）。1 なら必ず付与する
        [Label("付与確率")][Range(0f, 1f)]
        [SerializeField]
        private float mApplyChance = 1f;

        // 表示と判定で扱いを揃えるため、常に 0～1 へ丸めた値を使う
        private float ApplyChance => Mathf.Clamp01(mApplyChance);

        // 持続条件の上書き。未設定ならエフェクト定義側の設定に従う
        // 同じエフェクトを持続ターン数だけ変えて撃ち分けるためのもので、エフェクト ID は変わらない
        // （持続違いを別アセットにすると ID が別になり、本来リフレッシュされるべき効果が二重に付いてしまう）
        // 表示名を [Label] で与えると型選択ツリーが出せなくなるため [PPFieldLabel] を使う
        [PPFieldLabel("持続条件の上書き")]
        [SerializeReference]
        private PPDurationConditionDefinition mDurationOverride;

        // aSource : スキル発動者
        // aTarget : StatusEffect を付与する対象
        // aSourceSkill : この効果を保有するスキル定義
        // aContext : バトルコンテキスト
        public override void Apply(BattleUnit aSource, BattleUnit aTarget, PPSkillDefinition aSourceSkill, BattleContext aContext)
        {
            if (mEffect == null) return;
            if (!RollApplyChance(aTarget, aContext)) return;

            aTarget.AddStatusEffect(
                mEffect.CreateRuntimeStatusEffect(aSource, aTarget, aContext, mDurationOverride), aContext);
        }

        // 付与確率の抽選を行う
        // 乱数は再現性を保つため必ず Rules.RandomProvider を経由する
        // aTarget : 付与対象。ログ用
        // aContext : バトルコンテキスト
        // return : 付与する場合 true
        private bool RollApplyChance(BattleUnit aTarget, BattleContext aContext)
        {
            float chance = ApplyChance;
            if (chance >= 1f) return true;

            if (aContext.Rules.RandomProvider.NextFloat() < chance) return true;

            // 確率で外れた場合は何も起きないため、追えるようにログだけ残す
            CustomConsoleLog.Verbose("Battle",
                $"{aTarget?.DisplayName} への {mEffect.DisplayName} の付与に失敗しました（付与確率 {chance:P0}）。");
            return false;
        }

        // 付与するエフェクトの識別子を見積もりとして返す
        // AI はこれを対象の ActiveStatusEffects と突き合わせ、既に付いている場合の重ね掛けを避ける
        // aSource : スキル発動者
        // aTarget : StatusEffect を付与する対象
        // aContext : バトルコンテキスト
        // return : 付与エフェクトの識別子を持つ見積もり。未設定なら効果なし
        public override PPEffectEstimate Estimate(BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
            => mEffect != null ? PPEffectEstimate.FromStatus(mEffect.EffectId) : PPEffectEstimate.None;

        public override string BuildString()
        {
            if (mEffect == null) return "StatusEffect付与（未設定）";

            // 必中・上書きなしのときは付けない（大半のスキルがそうなので、毎行に付くと読みにくくなるため）
            string chance = ApplyChance >= 1f ? string.Empty : $"（{ApplyChance:P0}）";
            string duration = mDurationOverride != null ? $"［{mDurationOverride.BuildString()}］" : string.Empty;
            return $"StatusEffect付与{chance}{duration}：{mEffect.BuildString()}";
        }
    }
}
