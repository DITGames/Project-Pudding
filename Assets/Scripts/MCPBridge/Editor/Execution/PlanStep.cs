/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PlanStep.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief PlanExecutorが自律実行する1ステップ分のデータ
 * ToolCall(登録済みMCPツールの呼び出し)/WaitUntil(条件成立待ち)/Assert(条件検証)の3種
 * =====================================*/

using Newtonsoft.Json.Linq;

namespace MCPBridge.Editor.Execution
{
    public enum PlanStepType
    {
        ToolCall,
        WaitUntil,
        Assert,
    }

    public sealed class PlanStep
    {
        public string Id;
        public PlanStepType Type;

        // ToolCall時: MCPToolRegistryに登録済みのツール名
        public string ToolName;

        // ToolCall時: ツールへ渡す引数。WaitUntil/Assert時: MCPConditionEvaluatorが解釈する条件式
        public JObject Arguments;

        public float TimeoutSeconds = 10f;

        // ステップの実行開始時刻(EditorApplication.timeSinceStartup)。タイムアウト判定に使う
        public double? StartTime;
    }
}
