/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyPlan.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief パーティの行動計画
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    // ユニット1体分の行動割り当て
    public readonly struct PPPartyActionAssignment
    {
        public PPBattleUnit Unit { get; }
        public BattleCommandBase Command { get; }
        public int Order { get; }

        public PPPartyActionAssignment(PPBattleUnit aUnit, BattleCommandBase aCommand, int aOrder = 0)
        {
            Unit = aUnit;
            Command = aCommand;
            Order = aOrder;
        }
    }
    
    public sealed class PPPartyPlan
    {
        // 行動リスト
        public IReadOnlyList<PPPartyActionAssignment> Assignments { get; }
        // 溜める判断？
        public bool IsWait => Assignments.Count == 0;
        
        public static readonly PPPartyPlan Wait = new PPPartyPlan(Array.Empty<PPPartyActionAssignment>());
        public PPPartyPlan(IReadOnlyList<PPPartyActionAssignment> aAssignments) => Assignments = aAssignments;
    }
}