/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file GetLogsTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief CustomConsoleLog経由のタグ付きログを取得するMCPツール
 * 既存のCustomConsoleLogStore(Editor専用の静的ストア)をそのまま参照するラッパーであり、
 * CustomConsoleLog/CustomConsoleLogStore/CustomConsoleEntry自体は改変しない
 * =====================================*/

using System.Linq;
using CustomConsole.Editor;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;

namespace MCPBridge.Editor.Tools
{
    public sealed class GetLogsTool : IMCPTool
    {
        public string Name => "get_logs";

        public string Description => "CustomConsoleLog経由で出力されたタグ付きログを取得します(標準Debug.Logは対象外)。";

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

                var entries = CustomConsoleLogStore.Entries
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
