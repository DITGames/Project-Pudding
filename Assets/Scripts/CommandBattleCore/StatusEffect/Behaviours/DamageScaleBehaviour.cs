/* =====================================
 * Copyright hqrse. All rights reserved.
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

        public override int Order => mOrder;

        // 増幅は先(Order小)、軽減は後(Order大)に適用されるよう既定値を分けておく
        public DamageScaleBehaviour(float aScale, int aOrder = 0)
        {
            mScale = aScale;
            mOrder = aOrder;
        }

        public override void ModifyIncomingDamage(StatusEffectContext aContext, DamageInfo aDamage)
        {
            aDamage.Amount *= mScale;
            if (aDamage.Amount <= 0f) aDamage.IsNullified = true;
        }
    }
}
