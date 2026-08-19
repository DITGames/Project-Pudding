/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file CompileErrorWatcher.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 例外・コンパイルエラーを検知し、実行系(PlanExecutor)を中断させる
 * =====================================*/

using System.Linq;
using MCPBridge.Editor.Server;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace MCPBridge.Editor.Execution
{
    [InitializeOnLoad]
    public static class CompileErrorWatcher
    {
        static CompileErrorWatcher()
        {
            CompilationPipeline.assemblyCompilationFinished += HandleCompilationFinished;
            Application.logMessageReceivedThreaded += HandleLog;
        }

        // CompilationPipelineのコールバックはUnityのメインスレッドで発火するため直接呼んでよい
        private static void HandleCompilationFinished(string aAssembly, CompilerMessage[] aMessages)
        {
            if (!PlanExecutionState.IsRunning)
            {
                return;
            }

            var firstError = aMessages.FirstOrDefault(m => m.type == CompilerMessageType.Error);
            if (firstError.message != null)
            {
                PlanExecutionState.MarkError(null, $"コンパイルエラー({aAssembly}): {firstError.message}");
            }
        }

        // Application.logMessageReceivedThreadedはログが発生したスレッド上でそのまま発火するため、
        // PlanExecutionStateの読み書き(CurrentStepの参照・MarkErrorによるリスト書き換え)は
        // 必ずメインスレッドのキュー経由で行い、バックグラウンドスレッドから直接触らないようにする
        private static void HandleLog(string aCondition, string aStackTrace, LogType aType)
        {
            if (aType != LogType.Exception)
            {
                return;
            }

            MCPMainThreadDispatcher.Enqueue(() =>
            {
                if (PlanExecutionState.IsRunning)
                {
                    PlanExecutionState.MarkError(PlanExecutionState.CurrentStep, aCondition);
                }
            });
        }
    }
}
