/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file SimulateInputTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 新Input Systemの低レベルイベント(キーボード/マウス)をシミュレート送信するMCPツール
 * =====================================*/

using System;
using System.Linq;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MCPBridge.Editor.Tools
{
    public sealed class SimulateInputTool : IMCPTool
    {
        public string Name => "simulate_input";

        public string Description =>
            "キーボードまたはマウスの低レベル入力イベントをInput System経由でシミュレート送信します。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["device"] = new JObject { ["type"] = "string", ["enum"] = new JArray("keyboard", "mouse") },
                ["keys"] = new JObject { ["type"] = "array", ["description"] = "device=keyboard時に押下するKey名の配列" },
                ["position"] = new JObject { ["type"] = "object", ["description"] = "device=mouse時のスクリーン座標{x,y}" },
                ["buttons"] = new JObject { ["type"] = "integer", ["description"] = "device=mouse時のボタンビットマスク" },
            },
            ["required"] = new JArray("device"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                switch (aArguments.Value<string>("device"))
                {
                    case "keyboard":
                        SimulateKeyboard(aArguments);
                        break;
                    case "mouse":
                        SimulateMouse(aArguments);
                        break;
                    default:
                        throw new ArgumentException("deviceはkeyboardまたはmouseを指定してください。");
                }

                InputSystem.Update();
                return null;
            });
        }

        private static void SimulateKeyboard(JObject aArguments)
        {
            if (Keyboard.current == null)
            {
                throw new InvalidOperationException("Keyboardデバイスが見つかりません。");
            }

            var keys = (aArguments["keys"] as JArray)?
                .Select(k => (Key)Enum.Parse(typeof(Key), k.Value<string>()))
                .ToArray() ?? Array.Empty<Key>();

            var keyState = new KeyboardState(keys);
            InputSystem.QueueStateEvent(Keyboard.current, keyState);
        }

        private static void SimulateMouse(JObject aArguments)
        {
            if (Mouse.current == null)
            {
                throw new InvalidOperationException("Mouseデバイスが見つかりません。");
            }

            var positionToken = aArguments["position"];
            var mouseState = new MouseState
            {
                position = positionToken != null ? MCPArgumentConverter.ReadVector2(positionToken) : Mouse.current.position.ReadValue(),
                buttons = (ushort)(aArguments["buttons"]?.Value<int>() ?? 0),
            };
            InputSystem.QueueStateEvent(Mouse.current, mouseState);
        }
    }
}
