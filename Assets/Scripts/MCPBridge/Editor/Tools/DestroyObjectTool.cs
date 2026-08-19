/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file DestroyObjectTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief シーン上のGameObjectを削除するMCPツール
 * メモリ上のシーン状態のみを変更する(常時許可側に分類する)
 * =====================================*/

using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCPBridge.Editor.Tools
{
    public sealed class DestroyObjectTool : IMCPTool
    {
        public string Name => "destroy_object";

        public string Description => "objectPathで指定したGameObjectをシーンから削除します。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["objectPath"] = new JObject { ["type"] = "string" },
            },
            ["required"] = new JArray("objectPath"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var go = MCPSceneObjectResolver.ResolveGameObject(aArguments.Value<string>("objectPath"));
                Object.DestroyImmediate(go);
                return null;
            });
        }
    }
}
