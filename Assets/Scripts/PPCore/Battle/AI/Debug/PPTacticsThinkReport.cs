/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTacticsThinkReport.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 戦術AIの思考1回分の記録。デバッグウィンドウの表示元になる
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    // 戦術 1 件分の判定記録
    // 成立したものだけでなく不成立のものも理由付きで残す
    // 調整で知りたいのは「なぜその戦術が動いたか」より「なぜ動かなかったか」であることが多い
    public struct PPTacticsThinkEntry
    {
        public string TacticsName;
        // プロファイルの戦術リスト内の位置。小さいほど優先度が高い
        public int Priority;
        public bool IsExecutable;
        public PPTacticRejectReason RejectReason;
        // 進行位置と総ステップ数
        public int StepIndex;
        public int StepCount;
        public string ActorName;
        public string TargetName;
        public string ActionName;
        // 残クールタイム（ティック）
        public int RemainingCooldown;
        // 撃てるまでに掛かると見積もったティック数。今すぐ撃てる場合は 0
        public float EstimatedWaitTicks;
        // 今すぐ実行できるか。false なら溜めて待つ判断になっている
        public bool IsAffordableNow;
    }

    // 並行アクション 1 件分の判定記録
    // ステップと違って進行位置を持たないため、記録するのは「何回実行できたか」と
    // 「なぜそこで止まったか」の 2 点になる
    public struct PPTacticsParallelEntry
    {
        public string ActionName;
        // 実際に実行された回数
        public int ExecutedCount;
        // 設定された最大実行回数。0 以下は「尽きるまで」
        public int MaxExecutions;
        // ステップより先に実行する設定か
        public bool IsBeforeSteps;
        // 繰り返しを打ち切った理由。上限まで回りきった場合は ExecutionLimit
        public PPTacticRejectReason StopReason;
    }

    // 戦術 AI の思考 1 回分の記録
    // 「成立した戦術 → メイン戦術 → ステップ進行」という流れをそのまま追えるようにしてある
    // リアルタイムに進むバトルでは見たい瞬間を捉えられないため、
    // デバッグウィンドウ側がこれを複数件ためて遡れるようにする
    public sealed class PPTacticsThinkReport
    {
        // どちらの陣営の思考か
        public BattleSide Side;
        // 記録時点の経過ターン数
        public int TurnCount;
        // 記録時刻（Time.time）。ウィンドウでの並び順と識別に使う
        public float Timestamp;
        // 選ばれたメイン戦術の名前。選ばれなかった場合は待機の旨を表す
        public string MainTacticsName;
        // メイン戦術が選ばれた理由（新規選出 / 進行継続 / 割り込み / 候補なし）
        public string MainSelectReason;
        // リソース推移の平均増加量。待機判定の見積もりに使った値
        public float AverageGainPerTick;
        // 採用された行動の数
        public int AdoptedCount;
        // 全戦術の判定記録
        public List<PPTacticsThinkEntry> Tactics = new();
        // メイン戦術の並行アクションの判定記録
        public List<PPTacticsParallelEntry> ParallelActions = new();
    }
}
