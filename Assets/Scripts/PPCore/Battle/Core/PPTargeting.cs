/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTargeting.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief 種別/スコープ判定のユーティリティ
 * =====================================*/
using CommandBattleCore;

namespace PPCore
{
    public static class PPTargeting
    {
        public static bool NeedsManualTarget(TargetScope aScope)
            => aScope is TargetScope.SingleEnemy or TargetScope.SingleAlly;

        public static bool IsAllySide(TargetScope aScope)
            => aScope is TargetScope.SingleAlly or TargetScope.AllAllies or TargetScope.Self;
    }
}