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
    // ユニット 1 体分の行動割り当て。誰が・何を・どの順番で実行するかを 1 組にしたもの
    public readonly struct PPPartyActionAssignment
    {
        // 行動するユニット
        public PPBattleUnit Unit { get; }
        // 実行するコマンド
        public BattleCommandBase Command { get; }
        // 同一ティック内での実行順序。小さいほど先に実行される
        public int Order { get; }

        // aUnit : 行動するユニット
        // aCommand : 実行するコマンド
        // aOrder : 実行順序
        public PPPartyActionAssignment(PPBattleUnit aUnit, BattleCommandBase aCommand, int aOrder = 0)
        {
            Unit = aUnit;
            Command = aCommand;
            Order = aOrder;
        }
    }

    // 1 ティック分のパーティ行動計画
    // 割り当てが空であることが、そのまま「今回は動かずゲージを溜める」の意味になる
    // 待機のためだけの状態やフラグを持たせず、空リストで表現している
    public sealed class PPPartyPlan
    {
        // 採用された行動の割り当て。実行前に PPPartyActionAssignment.Order 順へ並べ替えられる
        public IReadOnlyList<PPPartyActionAssignment> Assignments { get; }
        // 待機（何もせずゲージを溜める）かどうか
        public bool IsWait => Assignments.Count == 0;

        // 待機を表す共有インスタンス。毎回の思考で新規生成しないためのもの
        public static readonly PPPartyPlan Wait = new PPPartyPlan(Array.Empty<PPPartyActionAssignment>());

        // aAssignments : 採用された行動の割り当て
        public PPPartyPlan(IReadOnlyList<PPPartyActionAssignment> aAssignments) => Assignments = aAssignments;
    }
}
