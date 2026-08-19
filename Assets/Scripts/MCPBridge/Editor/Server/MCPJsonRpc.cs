/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPJsonRpc.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief JSON-RPC 2.0のリクエスト型とMCPツール呼び出し例外
 * =====================================*/

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCPBridge.Editor.Server
{
    // MCPクライアントから送られてくるJSON-RPCリクエストの型
    public sealed class MCPRequest
    {
        [JsonProperty("jsonrpc")] public string JsonRpc = "2.0";
        [JsonProperty("id")] public JToken Id;
        [JsonProperty("method")] public string Method;
        [JsonProperty("params")] public JObject Params;
    }

    // ツール解決・実行時のエラーをJSON-RPCのエラーコードに対応付けて表現する例外
    public sealed class MCPToolException : Exception
    {
        public int Code { get; }

        public MCPToolException(int aCode, string aMessage) : base(aMessage)
        {
            Code = aCode;
        }
    }
}
