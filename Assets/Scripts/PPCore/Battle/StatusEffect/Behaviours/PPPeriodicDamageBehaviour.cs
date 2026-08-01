/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPeriodicDamageBehaviour.cs
 * @author hqrse
 * @date 2026/07/31
 * @brief 属性付きの継続ダメージ振る舞い
 * =====================================*/

using CommandBattleCore;

namespace PPCore
{
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
                mAmountPerStack * aContext.Stacks, PPSkillCategory.Debuff, mAttribute, aContext.Effect);
    }
}
