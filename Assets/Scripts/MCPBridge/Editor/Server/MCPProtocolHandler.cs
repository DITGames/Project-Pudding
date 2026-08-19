/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPProtocolHandler.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief JSON-RPCリクエストボディをMCPの各メソッド(initialize/tools/list/tools/call)にディスパッチする
 * =====================================*/

using System;
using System.Linq;
using MCPBridge.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCPBridge.Editor.Server
{
    public static class MCPProtocolHandler
    {
        private const string ProtocolVersion = "2024-11-05";
        private const string ServerName = "unity-editor-mcp-bridge";
        private const string ServerVersion = "0.1.0";

        // HTTPハンドラから呼ばれるエントリポイント。JSON-RPCレスポンス文字列(通知の場合は空文字)を返す
        public static string HandleRequestBody(string aBody)
        {
            MCPRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<MCPRequest>(aBody);
            }
            catch (Exception e)
            {
                return SerializeError(null, -32700, $"Parse error: {e.Message}");
            }

            if (request == null || string.IsNullOrEmpty(request.Method))
            {
                return SerializeError(null, -32600, "Invalid Request");
            }

            // 通知(idを持たないメッセージ、例: notifications/initialized)には応答しない
            if (request.Method.StartsWith("notifications/", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            try
            {
                JToken result = request.Method switch
                {
                    "initialize" => HandleInitialize(),
                    "tools/list" => HandleToolsList(),
                    "tools/call" => HandleToolsCall(request.Params),
                    "ping" => new JObject(),
                    _ => throw new MCPToolException(-32601, $"Method not found: {request.Method}"),
                };
                return SerializeResult(request.Id, result);
            }
            catch (MCPToolException e)
            {
                return SerializeError(request.Id, e.Code, e.Message);
            }
            catch (Exception e)
            {
                return SerializeError(request.Id, -32000, e.Message);
            }
        }

        private static JToken HandleInitialize()
        {
            return new JObject
            {
                ["protocolVersion"] = ProtocolVersion,
                ["capabilities"] = new JObject { ["tools"] = new JObject() },
                ["serverInfo"] = new JObject { ["name"] = ServerName, ["version"] = ServerVersion },
            };
        }

        // 現在のモードで許可されているツールのみを返す(モード切替はUnity側のみで行われ、
        // 次にこのメソッドが呼ばれた時点で新しいモードの許可範囲が反映される)
        private static JToken HandleToolsList()
        {
            var tools = MCPToolRegistry.ListAllowedTools().Select(t => new JObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["inputSchema"] = t.InputSchema,
            });
            return new JObject { ["tools"] = new JArray(tools) };
        }

        // モードで許可されていないツールが呼ばれた場合はMCPToolRegistry.Call内でエラーになる
        private static JToken HandleToolsCall(JObject aParams)
        {
            var name = aParams?.Value<string>("name");
            var arguments = aParams?["arguments"] as JObject ?? new JObject();
            var result = MCPToolRegistry.Call(name, arguments);
            return MCPContentFormatter.ToToolResult(result);
        }

        private static string SerializeResult(JToken aId, JToken aResult)
        {
            var response = new JObject { ["jsonrpc"] = "2.0", ["id"] = aId, ["result"] = aResult };
            return response.ToString(Formatting.None);
        }

        private static string SerializeError(JToken aId, int aCode, string aMessage)
        {
            var response = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = aId,
                ["error"] = new JObject { ["code"] = aCode, ["message"] = aMessage },
            };
            return response.ToString(Formatting.None);
        }
    }
}
