/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file StatusEffectBehaviour.cs
 * @author hqrse
 * @date 2026/07/31
 * @brief ステータスエフェクトの振る舞い1つ分
 * =====================================*/

namespace CommandBattleCore
{
    // StatusEffect に差し込む振る舞いの基底
    // 必要なフックだけを override する。付与のたびに new して使う想定(インスタンスごとに状態を持ってよい)
    public abstract class StatusEffectBehaviour
    {
        // 同一フック内での実行順。小さいほど先に実行される
        public virtual int Order => 0;

        // 付与された瞬間
        public virtual void OnApply(StatusEffectContext aContext) { }
        // 除去された瞬間
        public virtual void OnRemove(StatusEffectContext aContext) { }
        // 更新のたび(ターン経過など)
        public virtual void OnTick(StatusEffectContext aContext) { }
        // スタック数が変化したとき
        public virtual void OnStackChanged(StatusEffectContext aContext) { }
        // 被ダメージ確定前の介入
        public virtual void ModifyIncomingDamage(StatusEffectContext aContext, DamageInfo aDamage) { }
    }
}
