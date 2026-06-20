/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleParty.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトルパーティインスタンス
 * 入れ替え前提で作ってる
 * =====================================*/
using System;
using System.Collections.Generic;
using System.Linq;

namespace CommandBattleCore
{
    public class BattleParty
    {
        public List<BattleUnit> ActiveMembers { get; } = new();
        public List<BattleUnit> ReserveMembers { get; } = new();
        
        // 入れ替えデリゲート（退場ユニット、参戦ユニット）
        public Action<BattleUnit, BattleUnit> OnSwapped { get; set; }

        public BattleParty(BattleSide aSide, IEnumerable<BattleUnit> aActiveMembers,
            IEnumerable<BattleUnit> aReserveMembers = null)
        {
            foreach (var unit in aActiveMembers)
            {
                unit.Side = aSide;
                ActiveMembers.Add(unit);
            }

            if (aReserveMembers != null)
            {
                foreach (var unit in aReserveMembers)
                {
                    unit.Side = aSide;
                    ReserveMembers.Add(unit);
                }
            }
        }

        // アクティブメンバーとリザーブメンバーの双方向入れ替え
        public bool SwapMember(BattleUnit aActiveUnit, BattleUnit aReserveUnit)
        {
            int activeIdx = ActiveMembers.IndexOf(aActiveUnit);
            int reserveIdx = ReserveMembers.IndexOf(aReserveUnit);
            if (activeIdx < 0 || reserveIdx < 0) return false;
            
            ActiveMembers[activeIdx] = aReserveUnit;
            ReserveMembers[reserveIdx] = aActiveUnit;
            OnSwapped?.Invoke(aActiveUnit, aReserveUnit);
            return true;
        }

        // 生きているアクティブメンバーの取得
        public List<BattleUnit> GetAliveActiveMembers() => ActiveMembers.Where(u => u.IsAlive).ToList();
        
        // 全滅判定。アクティブ全員をチェック
        public virtual bool IsWiped() => ActiveMembers.All(u => !u.IsAlive);
    }
}