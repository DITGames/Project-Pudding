/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file FindObjectTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 階層パスからGameObjectを検索し、基本情報を返すMCPツール
 * =====================================*/

using System.Linq;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCPBridge.Editor.Tools
{
    public sealed class FindObjectTool : IMCPTool
    {
        public string Name => "find_object";

        public string Description =>
            "階層パスを指定してGameObjectを検索し、名前・アクティブ状態・アタッチされているコンポーネント一覧を返します。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["objectPath"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "階層パス(例: Parent/Child)",
                },
            },
            ["required"] = new JArray("objectPath"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var go = MCPSceneObjectResolver.ResolveGameObject(aArguments.Value<string>("objectPath"));
                return new JObject
                {
                    ["name"] = go.name,
                    ["activeSelf"] = go.activeSelf,
                    ["components"] = new JArray(go.GetComponents<Component>().Select(c => c.GetType().FullName)),
                };
            });
        }
    }
}
