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
    /// <summary>
    /// ユニット 1 体分の行動割り当て。誰が・何を・どの順番で実行するかを 1 組にしたもの。
    /// </summary>
    public readonly struct PPPartyActionAssignment
    {
        /// <summary>行動するユニット。</summary>
        public PPBattleUnit Unit { get; }
        /// <summary>実行するコマンド。</summary>
        public BattleCommandBase Command { get; }
        /// <summary>同一ティック内での実行順序。小さいほど先に実行される。</summary>
        public int Order { get; }

        /// <param name="aUnit">行動するユニット。</param>
        /// <param name="aCommand">実行するコマンド。</param>
        /// <param name="aOrder">実行順序。</param>
        public PPPartyActionAssignment(PPBattleUnit aUnit, BattleCommandBase aCommand, int aOrder = 0)
        {
            Unit = aUnit;
            Command = aCommand;
            Order = aOrder;
        }
    }

    /// <summary>
    /// 1 ティック分のパーティ行動計画。
    /// <para>
    /// 割り当てが空であることが、そのまま「今回は動かずリソースを溜める」の意味になる。
    /// 待機のためだけの状態やフラグを持たせず、空リストで表現している。
    /// </para>
    /// </summary>
    public sealed class PPPartyPlan
    {
        /// <summary>採用された行動の割り当て。実行前に <see cref="PPPartyActionAssignment.Order"/> 順へ並べ替えられる。</summary>
        public IReadOnlyList<PPPartyActionAssignment> Assignments { get; }
        /// <summary>待機（何もせずリソースを溜める）かどうか。</summary>
        public bool IsWait => Assignments.Count == 0;

        /// <summary>待機を表す共有インスタンス。毎回の思考で新規生成しないためのもの。</summary>
        public static readonly PPPartyPlan Wait = new PPPartyPlan(Array.Empty<PPPartyActionAssignment>());

        /// <param name="aAssignments">採用された行動の割り当て。</param>
        public PPPartyPlan(IReadOnlyList<PPPartyActionAssignment> aAssignments) => Assignments = aAssignments;
    }
}
