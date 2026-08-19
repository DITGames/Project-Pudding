/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PlayModeControlTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief Play/Pause/Stopを制御するMCPツール
 * =====================================*/

using System;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MCPBridge.Editor.Tools
{
    public sealed class PlayModeControlTool : IMCPTool
    {
        public string Name => "play_mode_control";

        public string Description => "Play/Pause/Resume/Stop、または現在の状態取得を行います。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["action"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray("play", "stop", "pause", "resume", "status"),
                },
            },
            ["required"] = new JArray("action"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var action = aArguments.Value<string>("action");
                switch (action)
                {
                    case "play":
                        EditorApplication.isPlaying = true;
                        break;
                    case "stop":
                        EditorApplication.isPlaying = false;
                        break;
                    case "pause":
                        EditorApplication.isPaused = true;
                        break;
                    case "resume":
                        EditorApplication.isPaused = false;
                        break;
                    case "status":
                        break;
                    default:
                        throw new ArgumentException($"未対応のactionです: {action}");
                }

                return new JObject
                {
                    ["isPlaying"] = EditorApplication.isPlaying,
                    ["isPaused"] = EditorApplication.isPaused,
                };
            });
        }
    }
}
