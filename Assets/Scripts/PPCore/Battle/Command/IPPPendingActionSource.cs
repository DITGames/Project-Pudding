/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPPendingActionSource.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief 実行待ちの行動をAIへ見せる供給元
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    // まだ実行されていない行動を AI へ見せるための口
    //
    // 行動はティック終了時にまとめて積まれて実行されるため、
    // AI が思考している時点では BattleManager のコマンド列は空になっている
    // 「相手が次に何をしようとしているか」を条件で見るには、
    // 積まれる前の材料であるプレイヤーの予約と相手 AI の計画を直接読む必要がある
    //
    // 実装はバトルの進行役（PPBattleRunner など）が担う
    // 予約と計画の両方を握っているのがそこだけのため
    public interface IPPPendingActionSource
    {
        // 指定した陣営の、まだ実行されていない行動を列挙する
        // aSide : 調べる陣営
        // return : 実行待ちの行動。無ければ空
        IEnumerable<PPPendingAction> EnumeratePending(BattleSide aSide);
    }
}
