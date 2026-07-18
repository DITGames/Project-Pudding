/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyAIContext.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief AIの戦略評価用コンテキスト
 * =====================================*/
using System.Collections.Generic;
using CommandBattleCore;
using Mono.Cecil;

namespace PPCore
{
    public sealed class PPPartyAIContext
    {
        public PPBattleParty Party { get; private set; }
        public BattleContext Context { get; private set; }
        
        public List<PPBattleUnit> AliveMembers { get; } = new();
        public List<PPBattleUnit> AliveEnemies { get; } = new();
        
        public PPBattleResourcePool ResourcePool { get; private set; }
        public float Current(PPResourceType aType) => ResourcePool.Current(aType);
        
        public PPBattleUnit LowestHpEnemy { get; private set; }
        public PPBattleUnit LowestHpRatioAlly { get; private set; }
        public float LowestAllyHpRatio { get; private set; } = 1f;

        public static PPPartyAIContext Capture(PPBattleParty aParty, BattleContext aContext)
        {
            var snap = new PPPartyAIContext { Party = aParty, Context = aContext };
            snap.ResourcePool = aParty.ResourcePool;
            
            // 味方パーティの集計
            foreach (var u in aParty.ActiveMembers)
            {
                if(u is not PPBattleUnit pp || !pp.IsAlive)
                    continue;
                snap.AliveMembers.Add(pp);
                
                float ratio = HpRatio(pp);
                if (ratio < snap.LowestAllyHpRatio)
                {
                    snap.LowestAllyHpRatio = ratio;
                    snap.LowestHpRatioAlly = pp;
                }
            }
            
            // 敵パーティの集計
            var opponent = ReferenceEquals(aParty, aContext.EnemyParty)
                ? aContext.AllyParty
                : aContext.EnemyParty;
            
            float lowestHp = float.MaxValue;
            foreach (var e in opponent.GetAliveActiveMembers())
            {
                if(e is not PPBattleUnit pp || !pp.IsAlive)
                    continue;
                snap.AliveEnemies.Add(pp);
                float hp = e.Parameters.Hp.CurrentValue;
                if (hp < lowestHp)
                {
                    lowestHp = hp;
                    snap.LowestHpEnemy = pp;
                }
            }
            return snap;
        }

        public static float HpRatio(PPBattleUnit aUnit)
        {
            float max = aUnit.Parameters.Hp.Max.CurrentValue;
            return max <= 0f ? 0f : aUnit.Parameters.Hp.CurrentValue / max;
        }
    }
}