/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ITargetResolver.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ターゲット解決の差し替え
 * =====================================*/
using System.Collections.Generic;

namespace CommandBattleCore
{
    public interface ITargetResolver
    {
        List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext);
    }
}