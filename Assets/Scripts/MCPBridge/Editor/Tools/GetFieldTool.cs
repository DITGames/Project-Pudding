/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file GetFieldTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief GameObjectのコンポーネント上のフィールド/プロパティの値を読み取るMCPツール
 * =====================================*/

using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;

namespace MCPBridge.Editor.Tools
{
    public sealed class GetFieldTool : IMCPTool
    {
        public string Name => "get_field";

        public string Description =>
            "objectPath+componentTypeで解決した対象のフィールド/プロパティの現在値を読み取ります。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["objectPath"] = new JObject { ["type"] = "string" },
                ["componentType"] = new JObject { ["type"] = "string" },
                ["field"] = new JObject { ["type"] = "string" },
            },
            ["required"] = new JArray("objectPath", "componentType", "field"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var target = MCPSceneObjectResolver.ResolveComponentOrGameObject(
                    aArguments.Value<string>("objectPath"),
                    aArguments.Value<string>("componentType"));

                var value = MCPReflectionAccessor.GetValue(target, aArguments.Value<string>("field"));
                return MCPArgumentConverter.ToJToken(value);
            });
        }
    }
}
