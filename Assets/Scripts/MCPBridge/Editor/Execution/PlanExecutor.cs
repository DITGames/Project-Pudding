/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PlanExecutor.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief PlanStep列をEditorApplication.update駆動で自律実行する決定的スクリプト実行系
 * LLMの往復を挟まず、フレーム単位で高速にステップを進める。
 * 例外・コンパイルエラー検知時(CompileErrorWatcher経由)はPlanExecutionState.MarkErrorにより
 * 即座に実行が中断される
 * =====================================*/

using System;
using System.Collections.Generic;
using MCPBridge.Editor.Tools;
using UnityEditor;

namespace MCPBridge.Editor.Execution
{
    [InitializeOnLoad]
    public static class PlanExecutor
    {
        static PlanExecutor()
        {
            EditorApplication.update += Tick;
        }

        // execute_planツールから呼ばれる。メインスレッド上で呼ばれる想定(MCPMainThreadDispatcher経由)
        public static void Submit(List<PlanStep> aSteps)
        {
            PlanExecutionState.Reset(aSteps);
        }

        private static void Tick()
        {
            if (!PlanExecutionState.IsRunning)
            {
                return;
            }

            var step = PlanExecutionState.CurrentStep;
            if (step == null)
            {
                return;
            }

            step.StartTime ??= EditorApplication.timeSinceStartup;

            try
            {
                if (ExecuteStep(step, out var finished) && finished)
                {
                    PlanExecutionState.AdvanceToNextStep();
                    return;
                }
            }
            catch (Exception e)
            {
                PlanExecutionState.MarkError(step, e.Message);
                return;
            }

            if (EditorApplication.timeSinceStartup - step.StartTime.Value > step.TimeoutSeconds)
            {
                PlanExecutionState.MarkError(step, $"タイムアウトしました({step.TimeoutSeconds}秒)");
            }
        }

        // ステップを1回分実行する。aFinishedがtrueの場合のみ次のステップへ進める
        // (WaitUntilは条件成立までfalseを返し続け、毎フレーム再評価される)
        private static bool ExecuteStep(PlanStep aStep, out bool aFinished)
        {
            switch (aStep.Type)
            {
                case PlanStepType.ToolCall:
                    MCPToolRegistry.Call(aStep.ToolName, aStep.Arguments);
                    aFinished = true;
                    return true;

                case PlanStepType.WaitUntil:
                    aFinished = MCPConditionEvaluator.Evaluate(aStep.Arguments);
                    return true;

                case PlanStepType.Assert:
                    if (!MCPConditionEvaluator.Evaluate(aStep.Arguments))
                    {
                        throw new InvalidOperationException("アサートに失敗しました。");
                    }
                    aFinished = true;
                    return true;

                default:
                    aFinished = true;
                    return true;
            }
        }
    }
}
