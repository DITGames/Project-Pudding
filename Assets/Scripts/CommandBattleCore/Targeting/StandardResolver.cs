/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file StandardResolver.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 
 * =====================================*/
using System;
using System.Collections.Generic;

namespace CommandBattleCore
{
    // 敵単体
    public class SingleEnemyResolver : ITargetResolver
    {
        public BattleUnit SelectedTarget { get; set;  }
        
        public SingleEnemyResolver(BattleUnit aSelectedTarget = null) => SelectedTarget = aSelectedTarget;

        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext)
        {
            // ターゲットが生存してるならそのまま送る
            if(SelectedTarget is {IsAlive: true})return new List<BattleUnit>{SelectedTarget};
            
            // 志望済みの場合は先頭を取得
            var alive = aContext.GetOpponentParty(aSource.Side).GetAliveActiveMembers();
            return aContext.Rules.DeadTargetPolicy.Fallback(aSource, SelectedTarget, alive, aContext);
        }
    }

    // 敵全体
    public class AllEnemiesResolver : ITargetResolver
    {
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext)
        => aContext.GetOpponentParty(aSource.Side).GetAliveActiveMembers();
    }

    // 味方単体
    public class SingleAllyResolver : ITargetResolver
    {
        public BattleUnit SelectedTarget { get; set; }
        public SingleAllyResolver(BattleUnit aSelectedTarget = null) => SelectedTarget = aSelectedTarget;

        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext)
        {
            if(SelectedTarget is {IsAlive: true})return new List<BattleUnit> { SelectedTarget };
            var alive = aContext.GetParty(aSource.Side).GetAliveActiveMembers();
            return aContext.Rules.DeadTargetPolicy.Fallback(aSource, SelectedTarget, alive, aContext);
        }
    }

    // 味方全体
    public class AllAlliesResolver : ITargetResolver
    {
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext) =>
            aContext.GetParty(aSource.Side).GetAliveActiveMembers();
    }

    // 敵ランダム
    public class RandomEnemyResolver : ITargetResolver
    {
        protected static readonly Random Rng = new();

        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext)
        {
            var alive = aContext.GetOpponentParty(aSource.Side).GetAliveActiveMembers();
            return alive.Count > 0
                ? new List<BattleUnit> { alive[Rng.Next(alive.Count)] }
                : new List<BattleUnit>();
        }
    }

    // セルフ
    public class SelfResolver : ITargetResolver
    {
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext) => new(){aSource};
    }
}