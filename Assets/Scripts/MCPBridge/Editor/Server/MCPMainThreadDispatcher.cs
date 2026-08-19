/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPMainThreadDispatcher.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief HTTPハンドラスレッドからUnityのメインスレッドへ処理を橋渡しする
 * Unity API(GameObject検索・リフレクション経由のコンポーネント操作等)はメインスレッド以外から
 * 呼ぶと例外やクラッシュの原因になるため、ツール実行は必ずEditorApplication.update側で行う
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MCPBridge.Editor.Server
{
    [InitializeOnLoad]
    public static class MCPMainThreadDispatcher
    {
        private static readonly Queue<Action> sQueue = new();
        private static readonly object sLock = new();
        private static readonly int sMainThreadId;

        static MCPMainThreadDispatcher()
        {
            sMainThreadId = Thread.CurrentThread.ManagedThreadId;
            EditorApplication.update += Pump;
        }

        // HTTPハンドラスレッドから呼ぶ。メインスレッドでaActionが完了するまでブロックし戻り値を返す。
        // 呼び出し時点で既にメインスレッド上(PlanExecutor等のEditorApplication.update経由)の場合、
        // Pump()を待つと自己デッドロックするため即座に実行する
        public static JToken RunOnMainThread(Func<JToken> aAction, int aTimeoutMs = 10000)
        {
            if (Thread.CurrentThread.ManagedThreadId == sMainThreadId)
            {
                return aAction();
            }

            using var done = new ManualResetEventSlim(false);
            JToken result = null;
            Exception error = null;

            Enqueue(() =>
            {
                try
                {
                    result = aAction();
                }
                catch (Exception e)
                {
                    error = e;
                }
                finally
                {
                    done.Set();
                }
            });

            if (!done.Wait(aTimeoutMs))
            {
                throw new TimeoutException("MCPツールの実行がタイムアウトしました。");
            }
            if (error != null)
            {
                throw error;
            }
            return result;
        }

        // execute_plan等、完了を待たずに投げっぱなしにしたい処理を積む
        public static void Enqueue(Action aAction)
        {
            lock (sLock)
            {
                sQueue.Enqueue(aAction);
            }
        }

        // メインスレッド(EditorApplication.update)側でキューを1フレーム分処理する
        private static void Pump()
        {
            while (true)
            {
                Action action;
                lock (sLock)
                {
                    if (sQueue.Count == 0)
                    {
                        return;
                    }
                    action = sQueue.Dequeue();
                }
                action();
            }
        }
    }
}
