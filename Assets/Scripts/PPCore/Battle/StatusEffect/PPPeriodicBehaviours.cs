/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPeriodicBehaviours.cs
 * @author hqrse
 * @date 2026/07/31
 * @brief 継続ダメージ・継続回復の振る舞い（定義とランタイム）
 * =====================================*/

using System;
using UnityEngine;
using UnityEngine.Serialization;
using AttributeUtility;

namespace PPCore
{
    // 毒・火傷など。効果の発生のたびに固定量のダメージを与える
    public class PeriodicDamageBehaviour : StatusEffectBehaviour
    {
        /* 状態 */

        protected readonly float mAmountPerStack;

        /* 生成 */

        public PeriodicDamageBehaviour(float aAmountPerStack) => mAmountPerStack = aAmountPerStack;

        // ダメージ情報の生成だけを差し替えられるようにしておく(PP側で属性付きにする等)
        protected virtual DamageInfo CreateDamage(StatusEffectContext aContext)
            => new DamageInfo(aContext.Source, aContext.Owner,
                mAmountPerStack * aContext.Stacks, new StatusEffectDamageReason(aContext.Effect));

        /* イベントハンドラ */

        public override void OnTick(StatusEffectContext aContext)
        {
            if (aContext.Owner == null || !aContext.Owner.IsAlive) return;
            aContext.Owner.ApplyDamage(CreateDamage(aContext), aContext.Battle);
        }
    }

    // リジェネなど。効果の発生のたびに固定量を回復する
    public sealed class PeriodicHealBehaviour : StatusEffectBehaviour
    {
        private readonly float mAmountPerStack;

        public PeriodicHealBehaviour(float aAmountPerStack) => mAmountPerStack = aAmountPerStack;

        public override void OnTick(StatusEffectContext aContext)
            => aContext.Owner?.ApplyHeal(mAmountPerStack * aContext.Stacks);
    }

    // 毒・火傷など、属性相性を伴う継続ダメージの振る舞い
    // ダメージ情報を属性付きの PPDamageInfo で生成する
    public sealed class PPPeriodicDamageBehaviour : PeriodicDamageBehaviour
    {
        private readonly PPTypeAttribute mAttribute;

        public PPPeriodicDamageBehaviour(float aAmountPerStack, PPTypeAttribute aAttribute)
            : base(aAmountPerStack) => mAttribute = aAttribute;

        protected override DamageInfo CreateDamage(StatusEffectContext aContext)
            => new PPDamageInfo(aContext.Source, aContext.Owner,
                mAmountPerStack * aContext.Stacks, mAttribute,
                new StatusEffectDamageReason(aContext.Effect));
    }

    // 対象の最大 HP に対する割合で、効果の発生のたびにダメージを与える振る舞い
    // 基準は現在 HP ではなく最大 HP（現在 HP 基準だと減るほど威力が落ちて倒し切れないため）
    public sealed class PPHpRatePeriodicDamageBehaviour : StatusEffectBehaviour
    {
        /* 状態 */

        // 1スタックあたりの最大HP割合（0～1）
        private readonly float mRatePerStack;
        private readonly PPTypeAttribute mAttribute;

        /* 生成 */

        // aRatePerStack : 1スタックあたりの最大HP割合（0～1）
        // aAttribute : ダメージの属性
        public PPHpRatePeriodicDamageBehaviour(float aRatePerStack, PPTypeAttribute aAttribute)
        {
            mRatePerStack = aRatePerStack;
            mAttribute = aAttribute;
        }

        // 対象の最大 HP とスタック数からダメージ情報を組み立てる
        // 四捨五入を正確に行うため decimal で計算する（Mathf.RoundToInt は 0.5 を偶数側へ丸めるため使わない）
        private DamageInfo CreateDamage(StatusEffectContext aContext)
        {
            decimal rawAmount = (decimal)aContext.Owner.Parameters.Hp.Max.CurrentValue
                              * (decimal)Mathf.Clamp01(mRatePerStack) * aContext.Stacks;
            float amount = (float)Math.Round(rawAmount, MidpointRounding.AwayFromZero);

            return new PPDamageInfo(aContext.Source, aContext.Owner, amount,
                mAttribute, new StatusEffectDamageReason(aContext.Effect));
        }

        /* イベントハンドラ */

        public override void OnTick(StatusEffectContext aContext)
        {
            if (aContext.Owner == null || !aContext.Owner.IsAlive) return;
            aContext.Owner.ApplyDamage(CreateDamage(aContext), aContext.Battle);
        }
    }

    // 効果の発生のたびに一定量のダメージを与える振る舞いの定義。発生の間隔は持続条件で決める
    // ダメージ量が対象に依存しないため、雑魚敵にもボスにも同じだけ効く
    [Serializable]
    [PPTypeMenuName("継続ダメージ/固定")]
    public class PPPeriodicDamageBehaviourDefinition : PPStatusEffectBehaviourDefinition
    {
        /* インスペクタ設定 */

        [Header("固定継続ダメージ")]
        [Label("1回あたりのダメージ量")][Min(0)]
        [FormerlySerializedAs("mDamagePerTurn")]
        [SerializeField]protected float mDamagePerTick = 5f;
        [Label("属性")]
        [SerializeField]protected PPTypeAttribute mAttribute = PPTypeAttribute.Normal;

        /* 初期化・接続 */

        public override void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new PPPeriodicDamageBehaviour(mDamagePerTick, mAttribute));
        }

        /* 説明文 */

        public override string BuildString()
            => $"継続ダメージ：{mDamagePerTick}/回（{mAttribute}）";
    }

    // 対象の最大HPに対する割合で、効果の発生のたびにダメージを与える振る舞いの定義
    // 最大HPが高い相手ほど効くため、HPの多いボスにも通る毒として使える
    [Serializable]
    [PPTypeMenuName("継続ダメージ/HP割合")]
    public class PPHpRatePeriodicDamageBehaviourDefinition : PPStatusEffectBehaviourDefinition
    {
        /* インスペクタ設定 */

        [Header("HP割合継続ダメージ")]
        // 対象の最大HPに対する割合。0.05 で最大HPの 5% になる
        [Label("1回あたりの割合")][Range(0f, 1f)]
        [FormerlySerializedAs("mHpRatePerTurn")]
        [SerializeField]protected float mHpRatePerTick = 0.05f;

        [Label("属性")]
        [SerializeField]protected PPTypeAttribute mAttribute = PPTypeAttribute.Normal;

        /* プロパティ */

        // 表示と実行で扱いを揃えるため、常に 0～1 へ丸めた値を使う
        protected float HpRatePerTick => Mathf.Clamp01(mHpRatePerTick);

        /* 初期化・接続 */

        public override void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new PPHpRatePeriodicDamageBehaviour(HpRatePerTick, mAttribute));
        }

        /* 説明文 */

        public override string BuildString()
            => $"継続ダメージ：最大HPの{HpRatePerTick:P0}/回（{mAttribute}）";
    }

    // 効果の発生のたびに一定量を回復させる振る舞いの定義（リジェネなど）
    [Serializable]
    [PPTypeMenuName("継続回復")]
    public class PPPeriodicHealBehaviourDefinition : PPStatusEffectBehaviourDefinition
    {
        [Header("継続回復")]
        [Label("1回あたりの回復量")]
        [FormerlySerializedAs("mHealPerTurn")]
        [SerializeField]protected float mHealPerTick = 5f;

        public override void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new PeriodicHealBehaviour(mHealPerTick));
        }

        public override string BuildString()
            => $"継続回復：{mHealPerTick}/回";
    }
}
