/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file CustomConsoleLog.cs
 * @author hqrse
 * @date 2026/07/13
 * @brief CustomConsoleWindow向けのログ出力API
 * タグ・拡張ログレベル(Verbose/Critical)・送信元オブジェクトを
 * メッセージに埋め込みつつDebug.Logを呼び出す。
 * 通常のDebug.Log/LogWarning/LogErrorもCustomConsoleWindowで捕捉されるが、
 * このAPI経由で出力するとカテゴリタグ・拡張レベル・送信元オブジェクトでの
 * フィルタ/ジャンプがより正確に機能する。
 * =====================================*/
using System.Text;
using UnityEngine;

namespace CustomConsole
{
    public enum CustomLogLevel
    {
        Verbose, Log, Warning, Error, Critical,
    }

    public static class CustomConsoleLog
    {
        // Emit直前に送信元Objectを通知する。CustomConsoleWindow(Editor)側が購読し、
        // 直後に発生するDebug.Logのログエントリと紐付ける
        public delegate void PendingContextHandler(Object aContext);
        public static event PendingContextHandler OnBeforeLog;

        public static void Verbose(string aTag, string aMessage, Object aContext = null) =>
            Emit(LogType.Log, "VERBOSE", aTag, aMessage, aContext);

        public static void Log(string aTag, string aMessage, Object aContext = null) =>
            Emit(LogType.Log, null, aTag, aMessage, aContext);

        public static void Warning(string aTag, string aMessage, Object aContext = null) =>
            Emit(LogType.Warning, null, aTag, aMessage, aContext);

        public static void Error(string aTag, string aMessage, Object aContext = null) =>
            Emit(LogType.Error, null, aTag, aMessage, aContext);

        public static void Critical(string aTag, string aMessage, Object aContext = null) =>
            Emit(LogType.Error, "CRITICAL", aTag, aMessage, aContext);

        private static void Emit(LogType aType, string aLevelMarker, string aTag, string aMessage, Object aContext)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(aLevelMarker))
            {
                builder.Append('[').Append(aLevelMarker).Append(']');
            }
            if (!string.IsNullOrEmpty(aTag))
            {
                builder.Append('[').Append(aTag).Append(']');
            }
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }
            builder.Append(aMessage);

            OnBeforeLog?.Invoke(aContext);

            switch (aType)
            {
                case LogType.Warning:
                    Debug.LogWarning(builder.ToString(), aContext);
                    break;
                case LogType.Error:
                    Debug.LogError(builder.ToString(), aContext);
                    break;
                default:
                    Debug.Log(builder.ToString(), aContext);
                    break;
            }
        }
    }
}
