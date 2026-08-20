/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPDefaultLogBuffer.cs
 * @author hqrse
 * @date 2026/08/20
 * @brief ログ実装が注入されなかった場合に使う既定のログ出力先・履歴保持
 * Application.logMessageReceivedThreadedを購読してリングバッファに履歴を溜める。
 * 出力はCustomConsoleLogと同じ"[タグ] 本文"形式でDebug.Logへ流すため、
 * CustomConsoleを導入しているプロジェクトではタグ付きのまま拾われる
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MCPBridge.Editor.Logging
{
    internal sealed class MCPDefaultLogBuffer : IMCPLogSink, IMCPLogSource
    {
        private const int MaxEntryCount = 1000;

        public static readonly MCPDefaultLogBuffer Instance = new();

        private readonly object mLock = new();
        private readonly Queue<MCPLogEntry> mEntries = new();

        private MCPDefaultLogBuffer()
        {
            // logMessageReceivedはメインスレッド発のログしか配信しない。MCPHttpServerの
            // ListenLoop/HandleRequestはバックグラウンドスレッドからログを出すため、
            // Threaded版を購読しないとHTTP例外ログがget_logsに出てこない。
            // HandleLogは他スレッドから呼ばれうるが、mLockで保護してある
            Application.logMessageReceivedThreaded += HandleLog;
        }

        public IReadOnlyList<MCPLogEntry> Entries
        {
            get
            {
                lock (mLock)
                {
                    return mEntries.ToList();
                }
            }
        }

        public void Write(MCPLogLevel aLevel, string aTag, string aMessage)
        {
            // Debug.Log経由で出すことでHandleLog側にも同じ内容が入るため、ここでバッファへは積まない。
            // 出力形式をCustomConsoleLogと揃えることで、CustomConsole側のタグ解析がそのまま効く
            var text = string.IsNullOrEmpty(aTag) ? aMessage : $"[{aTag}] {aMessage}";

            switch (aLevel)
            {
                case MCPLogLevel.Warning:
                    Debug.LogWarning(text);
                    break;
                case MCPLogLevel.Error:
                case MCPLogLevel.Critical:
                    Debug.LogError(text);
                    break;
                default:
                    Debug.Log(text);
                    break;
            }
        }

        private void HandleLog(string aCondition, string aStackTrace, LogType aType)
        {
            // MCPBridge以外が出したDebug.Logもここに入る。get_logsをCustomConsole非導入環境でも
            // 成立させるため、区別せず全件を履歴として保持する
            var (tag, message) = SplitTag(aCondition ?? string.Empty);

            lock (mLock)
            {
                mEntries.Enqueue(new MCPLogEntry(DateTime.Now, ToLevel(aType), tag, message));
                while (mEntries.Count > MaxEntryCount)
                {
                    mEntries.Dequeue();
                }
            }
        }

        // "[タグ] 本文" 形式ならタグ部分を切り出す。該当しなければタグ無しとして扱う
        private static (string Tag, string Message) SplitTag(string aCondition)
        {
            if (aCondition.Length == 0 || aCondition[0] != '[')
            {
                return (string.Empty, aCondition);
            }

            var close = aCondition.IndexOf(']');
            if (close < 0)
            {
                return (string.Empty, aCondition);
            }

            return (aCondition[1..close], aCondition[(close + 1)..].TrimStart());
        }

        private static MCPLogLevel ToLevel(LogType aType)
        {
            return aType switch
            {
                LogType.Warning => MCPLogLevel.Warning,
                LogType.Error or LogType.Exception or LogType.Assert => MCPLogLevel.Error,
                _ => MCPLogLevel.Log,
            };
        }
    }
}
