/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file LoadSceneTool.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief 指定シーンをロード/切替するMCPツール
 * 保存していない現在のシーンの変更を破棄しうる操作のため、
 * ツール利用モードではsave_scene等と同様に明示モードでのみ許可する
 * =====================================*/

using System;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;

namespace MCPBridge.Editor.Tools
{
    public sealed class LoadSceneTool : IMCPTool
    {
        public string Name => "load_scene";

        public string Description =>
            "指定パスのシーンをロードします(mode省略時はSingle。Additiveも指定できます)。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["scenePath"] = new JObject { ["type"] = "string" },
                ["mode"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray("single", "additive"),
                    ["description"] = "省略時はsingle",
                },
            },
            ["required"] = new JArray("scenePath"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var scenePath = aArguments.Value<string>("scenePath");
                var modeArg = aArguments.Value<string>("mode");
                var mode = string.Equals(modeArg, "additive", StringComparison.OrdinalIgnoreCase)
                    ? OpenSceneMode.Additive
                    : OpenSceneMode.Single;

                var scene = EditorSceneManager.OpenScene(scenePath, mode);
                if (!scene.IsValid())
                {
                    throw new InvalidOperationException($"シーンを開けませんでした: {scenePath}");
                }

                return new JObject { ["scenePath"] = scene.path, ["mode"] = mode.ToString() };
            });
        }
    }
}
