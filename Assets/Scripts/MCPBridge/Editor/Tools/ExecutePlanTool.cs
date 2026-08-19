/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ExecutePlanTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief PlanStep列を受け取りPlanExecutorへ実行を委譲するMCPツール
 * 完了を待たずに受理のみを即座に返す(実行本体はEditorApplication.update側で継続する)
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using MCPBridge.Editor.Execution;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;

namespace MCPBridge.Editor.Tools
{
    public sealed class ExecutePlanTool : IMCPTool
    {
        public string Name => "execute_plan";

        public string Description =>
            "計画エージェントが組み立てたステップ列を登録し、実行系(PlanExecutor)による自律実行を開始します。完了は待たず受理のみ返します。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["steps"] = new JObject
                {
                    ["type"] = "array",
                    ["description"] = "{id, type(ToolCall/WaitUntil/Assert), toolName, arguments, timeoutSeconds}の配列",
                },
            },
            ["required"] = new JArray("steps"),
        };

        public JToken Invoke(JObject aArguments)
        {
            var steps = (aArguments["steps"] as JArray)?.Select(ParseStep).ToList() ?? new List<PlanStep>();

            // Submit自体もPlanExecutionStateを書き換えるため、メインスレッドキューに積んで実行させる
            MCPMainThreadDispatcher.Enqueue(() => PlanExecutor.Submit(steps));

            return new JObject { ["accepted"] = true, ["stepCount"] = steps.Count };
        }

        private static PlanStep ParseStep(JToken aToken)
        {
            var obj = (JObject)aToken;
            return new PlanStep
            {
                Id = obj.Value<string>("id"),
                Type = (PlanStepType)Enum.Parse(typeof(PlanStepType), obj.Value<string>("type"), true),
                ToolName = obj.Value<string>("toolName"),
                Arguments = obj["arguments"] as JObject ?? new JObject(),
                TimeoutSeconds = obj["timeoutSeconds"]?.Value<float>() ?? 10f,
            };
        }
    }
}
