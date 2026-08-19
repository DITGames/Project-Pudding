/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file SetFieldTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief GameObjectのコンポーネント上のフィールド/プロパティへ値を書き込むMCPツール
 * メモリ上の状態のみを変更する(シーンファイルへは保存しない。保存にはsave_sceneツールを使う)
 * =====================================*/

using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;

namespace MCPBridge.Editor.Tools
{
    public sealed class SetFieldTool : IMCPTool
    {
        public string Name => "set_field";

        public string Description =>
            "objectPath+componentTypeで解決した対象のフィールド/プロパティへ値を書き込みます(メモリ上のみ)。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["objectPath"] = new JObject { ["type"] = "string" },
                ["componentType"] = new JObject { ["type"] = "string" },
                ["field"] = new JObject { ["type"] = "string" },
                ["value"] = new JObject(),
            },
            ["required"] = new JArray("objectPath", "componentType", "field", "value"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var target = MCPSceneObjectResolver.ResolveComponentOrGameObject(
                    aArguments.Value<string>("objectPath"),
                    aArguments.Value<string>("componentType"));

                MCPReflectionAccessor.SetValue(target, aArguments.Value<string>("field"), aArguments["value"]);
                return null;
            });
        }
    }
}
