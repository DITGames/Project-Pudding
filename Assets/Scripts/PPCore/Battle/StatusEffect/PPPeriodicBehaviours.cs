/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPeriodicBehaviours.cs
 * @author hqrse
 * @date 2026/07/31
 * @brief 継続ダメージ・継続回復の振る舞い（定義とランタイム）
 * =====================================*/

using System;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 毒・火傷など。更新のたびに固定量のダメージを与える
    public class PeriodicDamageBehaviour : StatusEffectBehaviour
    {
        protected readonly float mAmountPerStack;

        public PeriodicDamageBehaviour(float aAmountPerStack) => mAmountPerStack = aAmountPerStack;

        public override void OnTick(StatusEffectContext aContext)
        {
            if (aContext.Owner == null || !aContext.Owner.IsAlive) return;
            aContext.Owner.ApplyDamage(CreateDamage(aContext), aContext.Battle);
        }

        // ダメージ情報の生成だけを差し替えられるようにしておく(PP側で属性付きにする等)
        protected virtual DamageInfo CreateDamage(StatusEffectContext aContext)
            => new DamageInfo(aContext.Source, aContext.Owner,
                mAmountPerStack * aContext.Stacks, new StatusEffectDamageReason(aContext.Effect));
    }

    // リジェネなど。更新のたびに固定量を回復する
    public sealed class PeriodicHealBehaviour : StatusEffectBehaviour
    {
        private readonly float mAmountPerStack;

        public PeriodicHealBehaviour(float aAmountPerStack) => mAmountPerStack = aAmountPerStack;

        public override void OnTick(StatusEffectContext aContext)
            => aContext.Owner?.ApplyHeal(mAmountPerStack * aContext.Stacks);
    }

    // 毒・火傷など、属性相性を伴う継続ダメージの振る舞い
    // ダメージ情報を属性・スキル種別付きの PPDamageInfo で生成する
    public sealed class PPPeriodicDamageBehaviour : PeriodicDamageBehaviour
    {
        private readonly PPTypeAttribute mAttribute;

        public PPPeriodicDamageBehaviour(float aAmountPerStack, PPTypeAttribute aAttribute)
            : base(aAmountPerStack) => mAttribute = aAttribute;

        protected override DamageInfo CreateDamage(StatusEffectContext aContext)
            => new PPDamageInfo(aContext.Source, aContext.Owner,
                mAmountPerStack * aContext.Stacks, PPSkillCategory.Debuff, mAttribute,
                new StatusEffectDamageReason(aContext.Effect));
    }

    // 対象の最大 HP に対する割合で毎ターンダメージを与える振る舞い
    // 基準は現在 HP ではなく最大 HP（現在 HP 基準だと減るほど威力が落ちて倒し切れないため）
    public sealed class PPHpRatePeriodicDamageBehaviour : StatusEffectBehaviour
    {
        // 1スタックあたりの最大HP割合（0～1）
        private readonly float mRatePerStack;
        private readonly PPTypeAttribute mAttribute;

        // aRatePerStack : 1スタックあたりの最大HP割合（0～1）
        // aAttribute : ダメージの属性
        public PPHpRatePeriodicDamageBehaviour(float aRatePerStack, PPTypeAttribute aAttribute)
        {
            mRatePerStack = aRatePerStack;
            mAttribute = aAttribute;
        }

        public override void OnTick(StatusEffectContext aContext)
        {
            if (aContext.Owner == null || !aContext.Owner.IsAlive) return;
            aContext.Owner.ApplyDamage(CreateDamage(aContext), aContext.Battle);
        }

        // 対象の最大 HP とスタック数からダメージ情報を組み立てる
        // 四捨五入を正確に行うため decimal で計算する（Mathf.RoundToInt は 0.5 を偶数側へ丸めるため使わない）
        private DamageInfo CreateDamage(StatusEffectContext aContext)
        {
            decimal rawAmount = (decimal)aContext.Owner.Parameters.Hp.Max.CurrentValue
                              * (decimal)Mathf.Clamp01(mRatePerStack) * aContext.Stacks;
            float amount = (float)Math.Round(rawAmount, MidpointRounding.AwayFromZero);

            return new PPDamageInfo(aContext.Source, aContext.Owner, amount,
                PPSkillCategory.Debuff, mAttribute, new StatusEffectDamageReason(aContext.Effect));
        }
    }

    // 毎ターン一定量のダメージを与える振る舞いの定義
    // ダメージ量が対象に依存しないため、雑魚敵にもボスにも同じだけ効く
    [Serializable]
    [PPTypeMenuName("継続ダメージ/固定")]
    public class PPPeriodicDamageBehaviourDefinition : PPStatusEffectBehaviourDefinition
    {
        [Header("固定継続ダメージ")]
        [Label("毎ターンダメージ量")][Min(0)]
        [SerializeField]protected float mDamagePerTurn = 5f;
        [Label("属性")]
        [SerializeField]protected PPTypeAttribute mAttribute = PPTypeAttribute.Normal;

        public override void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new PPPeriodicDamageBehaviour(mDamagePerTurn, mAttribute));
        }

        public override string BuildString()
            => $"継続ダメージ：{mDamagePerTurn}/ターン（{mAttribute}）";
    }

    // 対象の最大HPに対する割合で毎ターンダメージを与える振る舞いの定義
    // 最大HPが高い相手ほど効くため、HPの多いボスにも通る毒として使える
    [Serializable]
    [PPTypeMenuName("継続ダメージ/HP割合")]
    public class PPHpRatePeriodicDamageBehaviourDefinition : PPStatusEffectBehaviourDefinition
    {
        [Header("HP割合継続ダメージ")]
        // 対象の最大HPに対する割合。0.05 で最大HPの 5% になる
        [Label("毎ターン割合")][Range(0f, 1f)]
        [SerializeField]protected float mHpRatePerTurn = 0.05f;
        [Label("属性")]
        [SerializeField]protected PPTypeAttribute mAttribute = PPTypeAttribute.Normal;

        // 表示と実行で扱いを揃えるため、常に 0～1 へ丸めた値を使う
        protected float HpRatePerTurn => Mathf.Clamp01(mHpRatePerTurn);

        public override void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new PPHpRatePeriodicDamageBehaviour(HpRatePerTurn, mAttribute));
        }

        public override string BuildString()
            => $"継続ダメージ：最大HPの{HpRatePerTurn:P0}/ターン（{mAttribute}）";
    }

    // 毎ターン一定量を回復させる振る舞いの定義（リジェネなど）
    [Serializable]
    [PPTypeMenuName("継続回復")]
    public class PPPeriodicHealBehaviourDefinition : PPStatusEffectBehaviourDefinition
    {
        [Header("継続回復")]
        [Label("毎ターン回復量")]
        [SerializeField]protected float mHealPerTurn = 5f;

        public override void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new PeriodicHealBehaviour(mHealPerTurn));
        }

        public override string BuildString()
            => $"継続回復：{mHealPerTurn}/ターン";
    }
}
