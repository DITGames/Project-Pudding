/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPMCPCustomConsoleBinding.cs
 * @author hqrse
 * @date 2026/08/20
 * @brief MCPBridgeのログ経路を本プロジェクトのCustomConsoleへ接続する
 * MCPBridge本体をプロジェクト非依存に保つため、CustomConsoleへの依存はこのファイルに閉じる。
 * MCPBridge配下にCustomConsoleへのusingを持ち込まないことがこの分離の目的
 * =====================================*/

using System.Collections.Generic;
using System.Linq;
using CustomConsole;
using CustomConsole.Editor;
using MCPBridge.Editor.Logging;
using UnityEditor;

namespace MCPBridgeBindings.Editor
{
    [InitializeOnLoad]
    internal static class PPMCPCustomConsoleBinding
    {
        static PPMCPCustomConsoleBinding()
        {
            MCPLog.SetSink(new PPMCPCustomConsoleSink());
            MCPLog.SetSource(new PPMCPCustomConsoleSource());
        }
    }

    // MCPBridgeからのログをCustomConsoleLogへ流す
    internal sealed class PPMCPCustomConsoleSink : IMCPLogSink
    {
        public void Write(MCPLogLevel aLevel, string aTag, string aMessage)
        {
            switch (aLevel)
            {
                case MCPLogLevel.Verbose:
                    CustomConsoleLog.Verbose(aTag, aMessage);
                    break;
                case MCPLogLevel.Warning:
                    CustomConsoleLog.Warning(aTag, aMessage);
                    break;
                case MCPLogLevel.Error:
                    CustomConsoleLog.Error(aTag, aMessage);
                    break;
                case MCPLogLevel.Critical:
                    CustomConsoleLog.Critical(aTag, aMessage);
                    break;
                default:
                    CustomConsoleLog.Log(aTag, aMessage);
                    break;
            }
        }
    }

    // get_logsツールへCustomConsoleLogStoreの履歴を渡す
    internal sealed class PPMCPCustomConsoleSource : IMCPLogSource
    {
        public IReadOnlyList<MCPLogEntry> Entries =>
            CustomConsoleLogStore.Entries
                .Select(e => new MCPLogEntry(e.Timestamp, ToMCPLevel(e.Level), e.Category, e.Message))
                .ToList();

        private static MCPLogLevel ToMCPLevel(CustomLogLevel aLevel)
        {
            return aLevel switch
            {
                CustomLogLevel.Verbose => MCPLogLevel.Verbose,
                CustomLogLevel.Warning => MCPLogLevel.Warning,
                CustomLogLevel.Error => MCPLogLevel.Error,
                CustomLogLevel.Critical => MCPLogLevel.Critical,
                _ => MCPLogLevel.Log,
            };
        }
    }
}
