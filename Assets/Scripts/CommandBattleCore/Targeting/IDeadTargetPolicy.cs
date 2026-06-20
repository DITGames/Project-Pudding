/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IDeadTargetPolicy.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 単体対象がいないときの代替選択方式
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace CommandBattleCore
{
    public interface IDeadTargetPolicy
    {
        List<BattleUnit> Fallback(BattleUnit aSource, BattleUnit aNoneTarget,
            List<BattleUnit> aAliveCandidates, BattleContext aContext);
    }

    // 戦闘ユニットを対象に置き換える
    public class FirstAliveFallback : IDeadTargetPolicy
    {
        public List<BattleUnit> Fallback(BattleUnit aSource, BattleUnit aNoneTarget, 
            List<BattleUnit> aAliveCandidates, BattleContext aContext)
                => aAliveCandidates.Count > 0 ? new List<BattleUnit> {aAliveCandidates[0]} : new List<BattleUnit>();
    }

    // 不発
    public class NoFallback : IDeadTargetPolicy
    {
        public List<BattleUnit> Fallback(BattleUnit aSource, BattleUnit aNoneTarget,
            List<BattleUnit> aAliveCandidates, BattleContext aContext)
            => new List<BattleUnit>();
    }
    
    // ランダム
    public class RandomFallback : IDeadTargetPolicy
    {
        public List<BattleUnit> Fallback(BattleUnit aSource, BattleUnit aNoneTarget,
            List<BattleUnit> aAliveCandidates, BattleContext aContext)
                => aAliveCandidates.Count > 0
                    ? new List<BattleUnit> {aAliveCandidates[aContext.Rules.RandomProvider.NextInt(aAliveCandidates.Count)]}
                    : new List<BattleUnit>();   
    }
}