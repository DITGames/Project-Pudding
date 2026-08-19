/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file GetInputStateTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 新Input Systemの現在の入力状態(キーボード/マウス)を観測するMCPツール
 * =====================================*/

using System;
using System.Linq;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEngine.InputSystem;

namespace MCPBridge.Editor.Tools
{
    public sealed class GetInputStateTool : IMCPTool
    {
        public string Name => "get_input_state";

        public string Description => "キーボードの押下中キー一覧、マウス座標・ボタン状態を返します。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject(),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var keyboard = Keyboard.current;
                var mouse = Mouse.current;

                var pressedKeys = keyboard == null
                    ? new JArray()
                    : new JArray(Enum.GetValues(typeof(Key)).Cast<Key>()
                        .Where(k => k != Key.None && keyboard[k].isPressed)
                        .Select(k => k.ToString()));

                var mousePosition = mouse == null
                    ? null
                    : new JObject { ["x"] = mouse.position.ReadValue().x, ["y"] = mouse.position.ReadValue().y };

                return new JObject
                {
                    ["pressedKeys"] = pressedKeys,
                    ["mousePosition"] = mousePosition,
                    ["mouseButtons"] = new JObject
                    {
                        ["left"] = mouse != null && mouse.leftButton.isPressed,
                        ["right"] = mouse != null && mouse.rightButton.isPressed,
                        ["middle"] = mouse != null && mouse.middleButton.isPressed,
                    },
                };
            });
        }
    }
}
