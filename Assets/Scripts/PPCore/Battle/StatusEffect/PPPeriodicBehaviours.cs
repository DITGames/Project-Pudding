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
    // 基底の PeriodicDamageBehaviour に対して、ダメージ情報の生成だけを
    // 属性・スキル種別付きの PPDamageInfo に差し替える
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

    // 対象の最大HPに対する割合で毎ターンダメージを与える振る舞い
    // 固定値の PPPeriodicDamageBehaviour と違い、最大HPが高い相手ほどダメージが大きくなる
    // 割合の基準は現在HPではなく最大HPとしている（現在HP基準だと減るほど威力が落ちて倒し切れないため）
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

        // 対象の最大HPとスタック数からダメージ情報を組み立てる
        // ダメージは整数へ四捨五入する（0.5 は 0 から遠い側へ丸める）
        // Mathf.RoundToInt は 0.5 を偶数側へ丸める（2.5 → 2）ため使わない
        // 計算は decimal で行う。float のまま掛けると 0.35 などが 0.3499999… になり、
        // ちょうど 0.5 になるはずの値が切り捨てられてしまうため
        // （float から decimal への変換は有効桁 7 桁で丸められるので、入力値の誤差もここで落ちる）
        // aContext : 振る舞いの実行コンテキスト
        // return : 与えるダメージの情報
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

        // aEffect : 組み立て先のエフェクト
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
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
        // （[Label] と [Range] が同居するとどちらの PropertyDrawer が使われるか保証されず、
        //   インスペクタ側でスライダーによる制限が効かない場合があるため）
        protected float HpRatePerTurn => Mathf.Clamp01(mHpRatePerTurn);

        // aEffect : 組み立て先のエフェクト
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
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

        // aEffect : 組み立て先のエフェクト
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        public override void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new PeriodicHealBehaviour(mHealPerTurn));
        }

        public override string BuildString()
            => $"継続回復：{mHealPerTurn}/ターン";
    }
}
