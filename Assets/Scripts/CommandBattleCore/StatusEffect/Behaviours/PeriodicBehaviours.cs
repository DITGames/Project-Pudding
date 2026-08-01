/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PeriodicBehaviours.cs
 * @author hqrse
 * @date 2026/07/31
 * @brief 継続ダメージ・継続回復の振る舞い
 * =====================================*/

namespace CommandBattleCore
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
                mAmountPerStack * aContext.Stacks, aContext.Effect);
    }

    // リジェネなど。更新のたびに固定量を回復する
    public sealed class PeriodicHealBehaviour : StatusEffectBehaviour
    {
        private readonly float mAmountPerStack;

        public PeriodicHealBehaviour(float aAmountPerStack) => mAmountPerStack = aAmountPerStack;

        public override void OnTick(StatusEffectContext aContext)
            => aContext.Owner?.ApplyHeal(mAmountPerStack * aContext.Stacks);
    }
}
