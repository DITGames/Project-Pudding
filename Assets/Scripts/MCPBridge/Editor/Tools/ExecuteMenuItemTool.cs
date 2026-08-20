/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ExecuteMenuItemTool.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief 指定したMenuItemパスを実行するMCPツール
 * EditorApplication.ExecuteMenuItemは存在しないパスを渡しても無反応・無例外を返す。
 * 実在パス一覧を事前検証するMenu.GetMenuItemDefaultShortcutsはUnity 6000.3.17f1には
 * 存在しない(コンパイルエラーで判明)ため、存在確認は行わず実行を試みるのみとする
 * (PLAN.md「確認事項」で想定していたフォールバック仕様)。
 * 破壊的operationに繋がりうる項目は固定の拒否リストでブロックする(コード修正でのみ変更可能)
 * =====================================*/

using System;
using System.Collections.Generic;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MCPBridge.Editor.Tools
{
    public sealed class ExecuteMenuItemTool : IMCPTool
    {
        // 拒否リストの追加・削除はコード修正でのみ行う(Window上や設定ファイルからの編集は提供しない)
        private static readonly HashSet<string> sDeniedMenuPaths = new(StringComparer.Ordinal)
        {
            "File/Quit",
            "File/Build Settings...",
            "File/Build Profiles...",
            "File/Build And Run",
            "Edit/Preferences...",
            "Edit/Project Settings...",
            "Assets/Delete",
        };

        public string Name => "execute_menu_item";

        public string Description =>
            "指定したMenuItemパスをUnity Editor上で実行します(拒否リストに該当する項目は実行しません。" +
            "存在しないパスを指定した場合もUnity側の仕様により無反応で成功扱いになります)。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["menuPath"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "例: \"Assets/Refresh\"",
                },
            },
            ["required"] = new JArray("menuPath"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var menuPath = aArguments.Value<string>("menuPath");
                if (sDeniedMenuPaths.Contains(menuPath))
                {
                    throw new MCPToolException(-32003, $"このMenuItemは拒否リストにより実行できません: {menuPath}");
                }

                // EditorApplication.ExecuteMenuItemは存在しないパスを渡しても例外・戻り値のいずれでも
                // 判別できないため、実行を試みるのみとする(存在確認はできない仕様上の制約)
                EditorApplication.ExecuteMenuItem(menuPath);
                return new JObject { ["executed"] = true, ["menuPath"] = menuPath };
            });
        }
    }
}
