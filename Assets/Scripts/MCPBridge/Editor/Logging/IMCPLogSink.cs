/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IMCPLogSink.cs
 * @author hqrse
 * @date 2026/08/20
 * @brief MCPBridgeからのログ出力先の抽象
 * 導入先プロジェクトが独自のログ基盤へ流し込めるよう、出力経路を差し替え可能にする
 * =====================================*/

namespace MCPBridge.Editor.Logging
{
    public interface IMCPLogSink
    {
        // ログを1件出力する
        // aLevel: ログレベル
        // aTag: 機能領域を表す大分類タグ
        // aMessage: 本文
        void Write(MCPLogLevel aLevel, string aTag, string aMessage);
    }
}
