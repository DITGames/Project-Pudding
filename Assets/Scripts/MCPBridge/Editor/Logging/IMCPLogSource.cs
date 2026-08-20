/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IMCPLogSource.cs
 * @author hqrse
 * @date 2026/08/20
 * @brief get_logsツールが読み取るログ履歴の抽象
 * 導入先プロジェクトが独自のログ基盤の履歴を返せるよう、取得経路を差し替え可能にする
 * =====================================*/

using System.Collections.Generic;

namespace MCPBridge.Editor.Logging
{
    public interface IMCPLogSource
    {
        // 保持しているログ履歴(古いものが先頭、新しいものが末尾)
        IReadOnlyList<MCPLogEntry> Entries { get; }
    }
}
