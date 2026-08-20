/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPLogEntry.cs
 * @author hqrse
 * @date 2026/08/20
 * @brief MCPBridgeが扱う1件分のログ
 * 各フィールドはget_logsツールが返すJSONのキーと1対1で対応する
 * =====================================*/

using System;

namespace MCPBridge.Editor.Logging
{
    public sealed class MCPLogEntry
    {
        public DateTime Timestamp { get; }
        public MCPLogLevel Level { get; }

        // 機能領域を表す大分類タグ。ログ実装がタグを持たない場合は空文字になる
        public string Category { get; }

        public string Message { get; }

        public MCPLogEntry(DateTime aTimestamp, MCPLogLevel aLevel, string aCategory, string aMessage)
        {
            Timestamp = aTimestamp;
            Level = aLevel;
            Category = aCategory ?? string.Empty;
            Message = aMessage ?? string.Empty;
        }
    }
}
