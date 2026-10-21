/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file DamageScaleBehaviour.cs
 * @author hqrse
 * @date 2026/07/31
 * @brief 被ダメージを増減させる振る舞い
 * =====================================*/

namespace CommandBattleCore
{
    public sealed class DamageScaleBehaviour : StatusEffectBehaviour
    {
        // 1.0で等倍、0.5で半減、0で無効化、1.5で1.5倍
        private readonly float mScale;
        private readonly int mOrder;
        // true なら状態異常によるダメージ（DamageReason.StatusEffect）には倍率を掛けない
        private readonly bool mIsExcludeStatusEffectDamage;

        public override int Order => mOrder;

        // 増幅は先(Order小)、軽減は後(Order大)に適用されるよう既定値を分けておく
        // aScale : 被ダメージ倍率
        // aOrder : 適用順
        // aIsExcludeStatusEffectDamage : 状態異常によるダメージを倍率の対象から外すか。既定は外さない（全ダメージに掛ける）
        public DamageScaleBehaviour(float aScale, int aOrder = 0, bool aIsExcludeStatusEffectDamage = false)
        {
            mScale = aScale;
            mOrder = aOrder;
            mIsExcludeStatusEffectDamage = aIsExcludeStatusEffectDamage;
        }

        public override void ModifyIncomingDamage(StatusEffectContext aContext, DamageInfo aDamage)
        {
            if (mIsExcludeStatusEffectDamage && aDamage.Reason == DamageReason.StatusEffect) return;

            aDamage.Amount *= mScale;
            if (aDamage.Amount <= 0f) aDamage.IsNullified = true;
        }
    }
}
