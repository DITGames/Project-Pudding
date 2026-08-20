/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file SearchAssetsTool.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief AssetDatabaseの検索フィルタでアセットパス一覧を取得するMCPツール
 * 読み取り専用のためDebugモードでも許可する
 * =====================================*/

using System.Linq;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MCPBridge.Editor.Tools
{
    public sealed class SearchAssetsTool : IMCPTool
    {
        public string Name => "search_assets";

        public string Description =>
            "AssetDatabase.FindAssets形式の検索フィルタ(例: \"t:Material MyMat\")でアセットを検索し、パス一覧を返します。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["filter"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "AssetDatabase.FindAssetsに渡す検索文字列(例: \"t:Texture2D\", \"Player t:Prefab\")",
                },
            },
            ["required"] = new JArray("filter"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var filter = aArguments.Value<string>("filter");
                var guids = AssetDatabase.FindAssets(filter);
                var paths = guids.Select(AssetDatabase.GUIDToAssetPath).Distinct();
                return new JArray(paths);
            });
        }
    }
}
