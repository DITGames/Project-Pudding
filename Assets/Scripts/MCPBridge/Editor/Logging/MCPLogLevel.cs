/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPLogLevel.cs
 * @author hqrse
 * @date 2026/08/20
 * @brief MCPBridgeが扱うログレベル
 * get_logsが返すlevel文字列をログ実装の差し替え前後で一致させるため、
 * CustomConsoleのCustomLogLevelと同じ並びにしてある
 * =====================================*/

namespace MCPBridge.Editor.Logging
{
    public enum MCPLogLevel
    {
        Verbose, Log, Warning, Error, Critical,
    }
}
