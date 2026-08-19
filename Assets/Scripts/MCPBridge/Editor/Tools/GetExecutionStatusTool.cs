/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file GetExecutionStatusTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief PlanExecutorの現在の進行状況を返すMCPツール(計画エージェントによるポーリング用)
 * =====================================*/

using MCPBridge.Editor.Execution;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;

namespace MCPBridge.Editor.Tools
{
    public sealed class GetExecutionStatusTool : IMCPTool
    {
        public string Name => "get_execution_status";

        public string Description => "execute_planで開始した実行系の現在の進行状況(ステータス・現在ステップ・エラー・ログ)を返します。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject(),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() => new JObject
            {
                ["status"] = PlanExecutionState.Status.ToString(),
                ["currentIndex"] = PlanExecutionState.CurrentIndex,
                ["stepCount"] = PlanExecutionState.Steps.Count,
                ["errorMessage"] = PlanExecutionState.ErrorMessage,
                ["log"] = new JArray(PlanExecutionState.LogEntries),
            });
        }
    }
}
