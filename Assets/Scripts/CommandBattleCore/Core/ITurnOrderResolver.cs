/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ITurnOrderResolver.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 行動順制御インターフェース
 * =====================================*/
using System.Collections.Generic;

namespace CommandBattleCore
{
    public interface ITurnOrderResolver
    {
        List<BattleUnit> ResolveOrder(BattleContext aContext);
    }
}