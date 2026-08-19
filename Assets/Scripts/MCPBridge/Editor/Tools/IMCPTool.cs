/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IMCPTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief MCPツールの共通インターフェース
 * =====================================*/

using Newtonsoft.Json.Linq;

namespace MCPBridge.Editor.Tools
{
    public interface IMCPTool
    {
        // tools/listで返すツール名(MCP上の一意なID)
        string Name { get; }

        // tools/listで返すツールの説明文
        string Description { get; }

        // tools/listで返す引数スキーマ(JSON Schema形式)
        JObject InputSchema { get; }

        // tools/callから呼ばれるツール本体の処理
        // aArguments: MCPクライアントから渡された引数
        // 戻り値: ツールの実行結果(MCPContentFormatterでcontent配列に変換される)
        JToken Invoke(JObject aArguments);
    }
}
