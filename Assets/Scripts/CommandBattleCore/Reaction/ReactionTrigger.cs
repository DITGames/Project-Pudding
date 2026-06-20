/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ReactionTrigger.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief リアクションの発動タイミング
 * =====================================*/

namespace CommandBattleCore
{
    public enum ReactionTrigger
    {
        OnDamaged,
        OnHealed,
        OnUnitDefeated,
        OnStatusAdded,
        OnTurnStarted,
        OnTurnEnded,
    }
}