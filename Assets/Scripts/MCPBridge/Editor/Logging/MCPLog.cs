/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPLog.cs
 * @author hqrse
 * @date 2026/08/20
 * @brief MCPBridge内部のログ出力・取得の窓口
 * SinkとSourceの注入口を提供し、未注入の間は既定実装(MCPDefaultLogBuffer)へフォールバックする。
 * 既定実装もタグ付き形式でDebug.Logへ出すため、注入の有無や[InitializeOnLoad]の
 * 実行順序に関わらずログが失われることはない
 * =====================================*/

namespace MCPBridge.Editor.Logging
{
    public static class MCPLog
    {
        private static IMCPLogSink sSink;
        private static IMCPLogSource sSource;

        public static IMCPLogSink Sink => sSink ??= MCPDefaultLogBuffer.Instance;

        public static IMCPLogSource Source => sSource ??= MCPDefaultLogBuffer.Instance;

        // 導入先プロジェクトから独自のログ出力先を注入する
        public static void SetSink(IMCPLogSink aSink)
        {
            sSink = aSink;
        }

        // 導入先プロジェクトから独自のログ履歴取得元を注入する
        public static void SetSource(IMCPLogSource aSource)
        {
            sSource = aSource;
        }

        public static void Verbose(string aTag, string aMessage) => Sink.Write(MCPLogLevel.Verbose, aTag, aMessage);

        public static void Log(string aTag, string aMessage) => Sink.Write(MCPLogLevel.Log, aTag, aMessage);

        public static void Warning(string aTag, string aMessage) => Sink.Write(MCPLogLevel.Warning, aTag, aMessage);

        public static void Error(string aTag, string aMessage) => Sink.Write(MCPLogLevel.Error, aTag, aMessage);
    }
}
