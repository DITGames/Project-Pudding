/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file MCPServerMenu.cs
 * @author hqrse
 * @date 2026/11/09
 * @brief MCPサーバーを手動で起動・停止するToolsメニュー
 * =====================================*/

using UnityEditor;

namespace MCPBridge.Editor.Server
{
    public static class MCPServerMenu
    {
        private const string StartPath = "Tools/MCP Bridge/MCPサーバーを起動";
        private const string StopPath = "Tools/MCP Bridge/MCPサーバーを停止";

        [MenuItem(StartPath)]
        private static void StartServer()
        {
            MCPHttpServer.Start();
        }

        // 起動中は起動項目を無効化する
        [MenuItem(StartPath, true)]
        private static bool ValidateStartServer()
        {
            return MCPHttpServer.State != MCPConnectionState.Listening;
        }

        [MenuItem(StopPath)]
        private static void StopServer()
        {
            MCPHttpServer.Stop();
        }

        // 起動していないときは停止項目を無効化する
        [MenuItem(StopPath, true)]
        private static bool ValidateStopServer()
        {
            return MCPHttpServer.State == MCPConnectionState.Listening;
        }
    }
}
