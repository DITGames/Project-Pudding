/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PlanExecutionState.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief PlanExecutorの進行状況・実行ログを保持する共有ステート
 * PlanExecutor(書き込み)、MCPBridgeWindow・GetExecutionStatusTool(読み取り)から参照される
 * =====================================*/

using System;
using System.Collections.Generic;

namespace MCPBridge.Editor.Execution
{
    public enum PlanRunStatus
    {
        Idle,
        Running,
        Completed,
        Error,
    }

    public static class PlanExecutionState
    {
        // 進行状況・ログの変化を通知する(MCPBridgeWindowのRepaintトリガーに使う)
        public static event Action OnChanged;

        public static PlanRunStatus Status { get; private set; } = PlanRunStatus.Idle;
        public static IReadOnlyList<PlanStep> Steps { get; private set; } = Array.Empty<PlanStep>();
        public static int CurrentIndex { get; private set; }
        public static string ErrorMessage { get; private set; }
        public static IReadOnlyList<string> LogEntries => sLogEntries;

        private static readonly List<string> sLogEntries = new();

        public static PlanStep CurrentStep =>
            CurrentIndex >= 0 && CurrentIndex < Steps.Count ? Steps[CurrentIndex] : null;

        public static bool IsRunning => Status == PlanRunStatus.Running;
        public static bool HasError => Status == PlanRunStatus.Error;

        // execute_planツールから呼ばれ、新しいステップ列で実行を開始する
        public static void Reset(List<PlanStep> aSteps)
        {
            Steps = aSteps;
            CurrentIndex = 0;
            Status = aSteps.Count > 0 ? PlanRunStatus.Running : PlanRunStatus.Completed;
            ErrorMessage = null;
            sLogEntries.Clear();
            Log($"実行開始: {aSteps.Count}ステップ");
            OnChanged?.Invoke();
        }

        public static void AdvanceToNextStep()
        {
            Log($"ステップ完了: {CurrentStep?.Id}");
            CurrentIndex++;
            if (CurrentIndex >= Steps.Count)
            {
                Status = PlanRunStatus.Completed;
                Log("全ステップ完了");
            }
            OnChanged?.Invoke();
        }

        // 例外・コンパイルエラー検知時、または条件検証(Assert)失敗時に呼ばれる
        public static void MarkError(PlanStep aStep, string aMessage)
        {
            Status = PlanRunStatus.Error;
            ErrorMessage = aMessage;
            Log($"エラー: {(aStep != null ? aStep.Id : "-")} - {aMessage}");
            OnChanged?.Invoke();
        }

        public static void Log(string aMessage)
        {
            sLogEntries.Add($"[{DateTime.Now:HH:mm:ss}] {aMessage}");
            OnChanged?.Invoke();
        }
    }
}
