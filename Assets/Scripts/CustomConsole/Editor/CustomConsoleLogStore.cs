/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file CustomConsoleLogStore.cs
 * @author hqrse
 * @date 2026/07/13
 * @brief Application.logMessageReceivedThreadedを購読し、
 * CustomConsoleWindowが表示するログ履歴を保持する静的ストア
 * ウィンドウを閉じていてもログを取り続ける
 * =====================================*/
using System;
using System.Collections.Generic;
using CustomConsole;
using UnityEditor;
using UnityEngine;

namespace CustomConsole.Editor
{
    [InitializeOnLoad]
    public static class CustomConsoleLogStore
    {
        private const int MaxEntryCount = 5000;
        private const string ClearOnPlayPrefsKey = "CustomConsole.ClearOnPlay";

        private static readonly object sLock = new();
        private static readonly List<CustomConsoleEntry> sEntries = new();
        private static readonly Queue<PendingLog> sPendingQueue = new();

        // Debug.Log直前にCustomConsoleLog.OnBeforeLogで通知された送信元Object。
        // 通知直後に同一スレッドで発生するHandleLogと同期させるために使用する
        [ThreadStatic] private static UnityEngine.Object sPendingContext;

        public static event Action OnEntriesChanged;

        public static IReadOnlyList<CustomConsoleEntry> Entries => sEntries;

        public static bool ClearOnPlay
        {
            get => EditorPrefs.GetBool(ClearOnPlayPrefsKey, false);
            set => EditorPrefs.SetBool(ClearOnPlayPrefsKey, value);
        }

        static CustomConsoleLogStore()
        {
            Application.logMessageReceivedThreaded += HandleLog;
            CustomConsoleLog.OnBeforeLog += aContext => sPendingContext = aContext;
            EditorApplication.update += Flush;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        public static void Clear()
        {
            lock (sLock)
            {
                sPendingQueue.Clear();
            }
            sEntries.Clear();
            OnEntriesChanged?.Invoke();
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange aChange)
        {
            if (aChange == PlayModeStateChange.ExitingEditMode && ClearOnPlay)
            {
                Clear();
            }
        }

        private static void HandleLog(string aCondition, string aStackTrace, LogType aType)
        {
            var context = sPendingContext;
            sPendingContext = null;

            lock (sLock)
            {
                sPendingQueue.Enqueue(new PendingLog(aCondition, aStackTrace, aType, context));
            }
        }

        private static void Flush()
        {
            List<PendingLog> pending;
            lock (sLock)
            {
                if (sPendingQueue.Count == 0)
                {
                    return;
                }
                pending = new List<PendingLog>(sPendingQueue);
                sPendingQueue.Clear();
            }

            foreach (var log in pending)
            {
                sEntries.Add(CustomConsoleEntry.Parse(log.Condition, log.StackTrace, log.Type, log.Context));
            }

            if (sEntries.Count > MaxEntryCount)
            {
                sEntries.RemoveRange(0, sEntries.Count - MaxEntryCount);
            }

            OnEntriesChanged?.Invoke();
        }

        private readonly struct PendingLog
        {
            public readonly string Condition;
            public readonly string StackTrace;
            public readonly LogType Type;
            public readonly UnityEngine.Object Context;

            public PendingLog(string aCondition, string aStackTrace, LogType aType, UnityEngine.Object aContext)
            {
                Condition = aCondition;
                StackTrace = aStackTrace;
                Type = aType;
                Context = aContext;
            }
        }
    }
}
