/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file GetLogsTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief タグ付きログを取得するMCPツール
 * ログ履歴の実体はIMCPLogSourceに委ねており、導入先プロジェクトが注入した
 * ログ基盤(本プロジェクトではCustomConsole)の履歴をそのまま返す
 * =====================================*/

using System.Linq;
using MCPBridge.Editor.Logging;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;

namespace MCPBridge.Editor.Tools
{
    public sealed class GetLogsTool : IMCPTool
    {
        public string Name => "get_logs";

        public string Description => "Unity Editorに出力されたタグ付きログを取得します。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["tag"] = new JObject { ["type"] = "string", ["description"] = "指定したカテゴリタグのみに絞り込む(省略時は全件)" },
                ["sinceIndex"] = new JObject { ["type"] = "integer", ["description"] = "このインデックス以降のログのみを返す(省略時は0)" },
            },
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var tagFilter = aArguments?.Value<string>("tag");
                var since = aArguments?["sinceIndex"]?.Value<int>() ?? 0;

                var entries = MCPLog.Source.Entries
                    .Skip(since)
                    .Where(e => string.IsNullOrEmpty(tagFilter) || e.Category == tagFilter)
                    .Select(e => new JObject
                    {
                        ["timestamp"] = e.Timestamp.ToString("O"),
                        ["level"] = e.Level.ToString(),
                        ["category"] = e.Category,
                        ["message"] = e.Message,
                    });

                return new JArray(entries);
            });
        }
    }
}
