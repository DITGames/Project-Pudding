/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPHttpServer.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief localhost限定のMCP HTTPサーバー本体
 * Streamable HTTPの簡易版として、POSTごとに単一のJSON-RPCレスポンスを返す(SSEは使わない)。
 * Editor起動時に自動的にリッスンを開始し、Editor終了時に停止する
 * =====================================*/

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using MCPBridge.Editor.Logging;
using UnityEditor;

namespace MCPBridge.Editor.Server
{
    public enum MCPConnectionState
    {
        Stopped,
        Listening,
        Error,
    }

    [InitializeOnLoad]
    public static class MCPHttpServer
    {
        public const int Port = 44378;
        private const string EndpointPath = "/mcp/";
        private const string LogTag = "MCPBridge";

        // 接続状態変化(Listening/Error/Stopped)を通知する(接続状態表示中心、値の詳細はErrorMessage/LastRequestReceivedAt参照)
        public static event Action OnConnectionStateChanged;

        public static MCPConnectionState State { get; private set; } = MCPConnectionState.Stopped;
        public static string LastErrorMessage { get; private set; }
        public static DateTime? LastRequestReceivedAt { get; private set; }

        // 直近にinitializeハンドシェイクを送ってきたMCPクライアントの情報(MCPBridgeWindowの表示用)。
        // ステートレスなHTTPサーバーのため厳密な「現在接続中」判定はできず、あくまで直近の情報として扱う
        public static string ConnectedClientName { get; private set; }
        public static string ConnectedClientVersion { get; private set; }

        private static HttpListener sListener;
        private static Thread sListenerThread;

        static MCPHttpServer()
        {
            Start();
            EditorApplication.quitting += Stop;
        }

        public static void Start()
        {
            if (State == MCPConnectionState.Listening)
            {
                return;
            }

            try
            {
                sListener = new HttpListener();
                sListener.Prefixes.Add($"http://localhost:{Port}{EndpointPath}");
                sListener.Start();

                // この世代のリスナーをクロージャでキャプチャして渡す。Restart()でsListenerが
                // 差し替わった後も、旧スレッドは自分が開始した世代のリスナーだけを見て判定できる
                var listener = sListener;
                sListenerThread = new Thread(() => ListenLoop(listener)) { IsBackground = true };
                sListenerThread.Start();

                SetState(MCPConnectionState.Listening, null);
                MCPLog.Log(LogTag, $"MCPサーバーを起動しました: http://localhost:{Port}{EndpointPath}");
            }
            catch (Exception e)
            {
                SetState(MCPConnectionState.Error, e.Message);
                MCPLog.Error(LogTag, $"MCPサーバーの起動に失敗しました: {e.Message}");
            }
        }

        public static void Stop()
        {
            try
            {
                sListener?.Stop();
                sListener?.Close();
            }
            catch (Exception)
            {
                // 既に閉じている場合等、Stop時の例外は無視してよい
            }
            SetState(MCPConnectionState.Stopped, null);
        }

        // MCPBridgeWindow上の再起動ボタンから呼ばれる。接続エラーからの手動復旧手段として提供する
        // (MCPクライアント側のツール一覧キャッシュまで再取得させられるかはクライアント実装依存であり、
        // Unity側のリスナーを起動し直すことまでしか保証できない)
        public static void Restart()
        {
            Stop();
            Start();
        }

        // バックグラウンドスレッドでHTTPリクエストを待ち受け続けるループ
        // LastRequestReceivedAt/State等の状態書き換えとイベント発火は、EditorWindow側がメインスレッド以外から
        // 触られることを想定していないため、必ずMCPMainThreadDispatcher経由でメインスレッドに委譲する。
        // aListenerはこのスレッドが開始した時点のリスナーをローカルに固定したもの(Start()から渡される)。
        // staticフィールドsListenerを直接参照すると、Restart()で新しい世代のリスナーに差し替わった後に
        // 旧スレッドがそれを誤って参照し、正常な再起動なのにエラー扱いしてしまう競合が起きるため避ける
        private static void ListenLoop(HttpListener aListener)
        {
            while (aListener is { IsListening: true })
            {
                try
                {
                    var context = aListener.GetContext();
                    var receivedAt = DateTime.Now;
                    MCPMainThreadDispatcher.Enqueue(() => LastRequestReceivedAt = receivedAt);
                    HandleRequest(context);
                }
                catch (HttpListenerException)
                {
                    // Stop()による意図的な中断はここに来るため、まだListening中の例外だけをエラー扱いにする
                    if (aListener is { IsListening: true })
                    {
                        MCPMainThreadDispatcher.Enqueue(() => SetState(MCPConnectionState.Error, "リスナーで例外が発生しました。"));
                    }
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        // 1リクエスト分の受信・ディスパッチ・応答書き込みを行う(バックグラウンドスレッド上)
        private static void HandleRequest(HttpListenerContext aContext)
        {
            try
            {
                string body;
                using (var reader = new StreamReader(aContext.Request.InputStream, aContext.Request.ContentEncoding))
                {
                    body = reader.ReadToEnd();
                }

                var responseJson = MCPProtocolHandler.HandleRequestBody(body);
                var buffer = Encoding.UTF8.GetBytes(responseJson);

                aContext.Response.ContentType = "application/json";
                aContext.Response.ContentLength64 = buffer.Length;
                aContext.Response.StatusCode = 200;
                aContext.Response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception e)
            {
                try
                {
                    aContext.Response.StatusCode = 500;
                }
                catch (Exception)
                {
                    // 既にヘッダを送信済みの場合はステータス変更できないため無視する
                }
                MCPLog.Error(LogTag, $"HTTPリクエスト処理中に例外が発生しました: {e}");
            }
            finally
            {
                aContext.Response.OutputStream.Close();
            }
        }

        private static void SetState(MCPConnectionState aState, string aErrorMessage)
        {
            State = aState;
            LastErrorMessage = aErrorMessage;
            OnConnectionStateChanged?.Invoke();
        }

        // MCPProtocolHandler.HandleInitializeから呼ばれる。メインスレッドから呼ぶこと
        // (MCPMainThreadDispatcher.Enqueue経由での呼び出しを前提とする)
        public static void RecordClientInfo(string aName, string aVersion)
        {
            ConnectedClientName = aName;
            ConnectedClientVersion = aVersion;
        }
    }
}
