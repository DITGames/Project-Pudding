/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file StatusEffectContext.cs
 * @author hqrse
 * @date 2026/07/31
 * @brief 振る舞いに渡す情報のまとまり
 * =====================================*/

namespace CommandBattleCore
{
    public readonly struct StatusEffectContext
    {
        // 対象のエフェクト本体
        public StatusEffect Effect { get; }
        // エフェクトがかかっているユニット
        public BattleUnit Owner { get; }
        // エフェクトを付与した側(不明な場合はnull)
        public BattleUnit Source { get; }
        // バトル全体の状況(乱数・ルールなど)。ApplyDamage(DamageInfo) 経由で渡された場合はnullになりうる
        public BattleContext Battle { get; }
        // 現在のスタック数
        public int Stacks => Effect.CurrentStacks;

        public StatusEffectContext(StatusEffect aEffect, BattleUnit aOwner, BattleContext aBattle)
        {
            Effect = aEffect;
            Owner = aOwner;
            Source = aEffect?.Source;
            Battle = aBattle;
        }
    }
}
