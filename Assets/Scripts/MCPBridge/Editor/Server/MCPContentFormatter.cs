/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPContentFormatter.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief ツールのInvoke戻り値をMCPのtools/callレスポンス形式(content配列)に変換する
 * =====================================*/

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCPBridge.Editor.Server
{
    public static class MCPContentFormatter
    {
        // 戻り値が {mimeType, base64} を持つ場合はMCPのimage contentへ、それ以外はtext contentへ変換する
        public static JObject ToToolResult(JToken aRawResult)
        {
            if (aRawResult is JObject obj && obj["base64"] != null && obj["mimeType"] != null)
            {
                return new JObject
                {
                    ["content"] = new JArray(new JObject
                    {
                        ["type"] = "image",
                        ["data"] = obj["base64"],
                        ["mimeType"] = obj["mimeType"],
                    }),
                };
            }

            var text = aRawResult == null || aRawResult.Type == JTokenType.Null
                ? "null"
                : aRawResult.ToString(Formatting.None);

            return new JObject
            {
                ["content"] = new JArray(new JObject { ["type"] = "text", ["text"] = text }),
            };
        }
    }
}
