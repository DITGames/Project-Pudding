/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPHpRatePeriodicDamageBehaviour.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief 最大HPの割合で継続ダメージを与える振る舞い
 * =====================================*/

using System;
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
                PPSkillCategory.Debuff, mAttribute, aContext.Effect, DamageReason.StatusEffect);
        }
    }
}
