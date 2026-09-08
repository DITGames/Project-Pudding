/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPHpRatePeriodicDamageBehaviour.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief 最大HPの割合で継続ダメージを与える振る舞い
 * =====================================*/

using UnityEngine;
using CommandBattleCore;

namespace PPCore
{
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
        // aContext : 振る舞いの実行コンテキスト
        // return : 与えるダメージの情報
        private DamageInfo CreateDamage(StatusEffectContext aContext)
        {
            float amount = aContext.Owner.Parameters.Hp.Max.CurrentValue
                         * Mathf.Clamp01(mRatePerStack) * aContext.Stacks;

            return new PPDamageInfo(aContext.Source, aContext.Owner, amount,
                PPSkillCategory.Debuff, mAttribute, aContext.Effect, DamageReason.StatusEffect);
        }
    }
}
