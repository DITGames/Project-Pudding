/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPBridgeWindow.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 接続状態・TODO進行状況・実行ログを表示する可視化パネル
 * TODOの手動編集・実行の中断/再開など、実行中の計画そのものへの介入は行わない(SPEC anti-goal)。
 * 一方でツール利用モードの切替・新規作成はSPECで明示的に許可された操作面のため、
 * このウィンドウ上のUIから行えるようにする
 * =====================================*/

using System;
using System.Linq;
using MCPBridge.Editor.Execution;
using MCPBridge.Editor.Mode;
using MCPBridge.Editor.Server;
using MCPBridge.Editor.Tools;
using UnityEditor;
using UnityEngine;

namespace MCPBridge.Editor.Window
{
    public class MCPBridgeWindow : EditorWindow
    {
        private Vector2 mExecutionLogScrollPosition;
        private Vector2 mSystemLogScrollPosition;

        [MenuItem("Window/MCP Bridge")]
        public static void Open()
        {
            var window = GetWindow<MCPBridgeWindow>();
            window.titleContent = new GUIContent("MCP Bridge");
            window.minSize = new Vector2(360, 420);
            window.Show();
        }

        // 接続状態・実行進行・モード・システムログの変化はイベント購読でRepaintする。
        // それとは別に「最終リクエスト受信時刻」表示だけはイベントを持たないため、
        // 毎フレームRepaintするのではなく低頻度(1秒間隔)のポーリングで更新する
        private const double PeriodicRepaintIntervalSeconds = 1.0;
        private double mNextPeriodicRepaintTime;

        private void OnEnable()
        {
            MCPHttpServer.OnConnectionStateChanged += Repaint;
            PlanExecutionState.OnChanged += Repaint;
            MCPModeRegistry.OnModeChanged += Repaint;
            MCPSystemEventLog.OnChanged += Repaint;
            EditorApplication.update += PeriodicRepaint;
        }

        private void PeriodicRepaint()
        {
            if (EditorApplication.timeSinceStartup < mNextPeriodicRepaintTime)
            {
                return;
            }
            mNextPeriodicRepaintTime = EditorApplication.timeSinceStartup + PeriodicRepaintIntervalSeconds;
            Repaint();
        }

        private void OnDisable()
        {
            MCPHttpServer.OnConnectionStateChanged -= Repaint;
            PlanExecutionState.OnChanged -= Repaint;
            MCPModeRegistry.OnModeChanged -= Repaint;
            MCPSystemEventLog.OnChanged -= Repaint;
            EditorApplication.update -= PeriodicRepaint;
        }

        private void OnGUI()
        {
            DrawConnectionStatus();
            EditorGUILayout.Space();
            DrawModeSection();
            EditorGUILayout.Space();
            DrawTodoProgress();
            EditorGUILayout.Space();
            DrawExecutionLog();
            EditorGUILayout.Space();
            DrawSystemEventLog();
        }

        private void DrawConnectionStatus()
        {
            EditorGUILayout.LabelField("接続状態", EditorStyles.boldLabel);

            switch (MCPHttpServer.State)
            {
                case MCPConnectionState.Listening:
                    var lastRequest = MCPHttpServer.LastRequestReceivedAt;
                    var lastRequestText = lastRequest.HasValue
                        ? lastRequest.Value.ToString("HH:mm:ss")
                        : "まだリクエストがありません";
                    EditorGUILayout.HelpBox(
                        $"待受中: http://localhost:{MCPHttpServer.Port}\n最終リクエスト受信: {lastRequestText}",
                        MessageType.Info);
                    break;
                case MCPConnectionState.Error:
                    EditorGUILayout.HelpBox(
                        $"サーバーでエラーが発生しました。再接続はUnity Editorの再起動、または手動操作が必要です。\n{MCPHttpServer.LastErrorMessage}",
                        MessageType.Error);
                    break;
                case MCPConnectionState.Stopped:
                default:
                    EditorGUILayout.HelpBox("未接続です。", MessageType.Warning);
                    break;
            }

            // 接続エラー等からの手動復旧手段。MCPクライアント側のツール一覧キャッシュまでは
            // 再取得させられない可能性がある(Unity側リスナーの起動し直しのみ保証)
            if (GUILayout.Button("MCPサーバーを再起動", GUILayout.Width(160)))
            {
                MCPHttpServer.Restart();
            }
        }

        private void DrawModeSection()
        {
            EditorGUILayout.LabelField("ツール利用モード", EditorStyles.boldLabel);

            var modeNames = MCPModeRegistry.Modes.Select(m => m.Name).ToArray();
            var currentIndex = Array.IndexOf(modeNames, MCPModeRegistry.CurrentMode.Name);

            EditorGUILayout.BeginHorizontal();
            var newIndex = EditorGUILayout.Popup(currentIndex, modeNames);
            if (newIndex != currentIndex && newIndex >= 0)
            {
                MCPModeRegistry.SwitchTo(modeNames[newIndex]);
            }

            if (GUILayout.Button("新規モード作成", GUILayout.Width(120)))
            {
                MCPModeCreateWindow.Open(MCPToolRegistry.AllToolNames);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                $"許可ツール数: {MCPModeRegistry.CurrentMode.AllowedToolNames.Count} / {MCPToolRegistry.AllToolNames.Count()}",
                EditorStyles.miniLabel);
        }

        private void DrawTodoProgress()
        {
            EditorGUILayout.LabelField("実行進行状況", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"ステータス: {PlanExecutionState.Status}");

            var steps = PlanExecutionState.Steps;
            if (steps.Count == 0)
            {
                EditorGUILayout.LabelField("実行中のステップはありません。");
                return;
            }

            for (var i = 0; i < steps.Count; i++)
            {
                var marker = i < PlanExecutionState.CurrentIndex ? "[完了]"
                    : i == PlanExecutionState.CurrentIndex ? "[実行中]"
                    : "[待機]";
                EditorGUILayout.LabelField($"{marker} {steps[i].Id} ({steps[i].Type})");
            }

            if (PlanExecutionState.HasError)
            {
                EditorGUILayout.HelpBox(PlanExecutionState.ErrorMessage, MessageType.Error);
            }
        }

        private void DrawExecutionLog()
        {
            EditorGUILayout.LabelField("実行ログ", EditorStyles.boldLabel);
            mExecutionLogScrollPosition = EditorGUILayout.BeginScrollView(mExecutionLogScrollPosition, GUILayout.Height(100));
            foreach (var entry in PlanExecutionState.LogEntries)
            {
                EditorGUILayout.LabelField(entry, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSystemEventLog()
        {
            EditorGUILayout.LabelField("システムイベントログ(モード切替等)", EditorStyles.boldLabel);
            mSystemLogScrollPosition = EditorGUILayout.BeginScrollView(mSystemLogScrollPosition, GUILayout.Height(80));
            foreach (var entry in MCPSystemEventLog.Entries)
            {
                EditorGUILayout.LabelField(entry, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
