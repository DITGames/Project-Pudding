/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ITargetFilter.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief TargetResolverが解決した対象を加工するフィルタ
 * =====================================*/
using System.Collections.Generic;

namespace CommandBattleCore
{
    public interface ITargetFilter
    {
        List<BattleUnit> Filter(BattleUnit aSource, List<BattleUnit> aCandidates, BattleContext aContext);
    }
}