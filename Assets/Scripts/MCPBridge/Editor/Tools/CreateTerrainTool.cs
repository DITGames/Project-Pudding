/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file CreateTerrainTool.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 新規TerrainDataアセットを作成し、それを使ったTerrainをシーンへ生成するMCPツール
 * TerrainDataアセットの新規作成(ディスクへの永続化)を伴うため、
 * ツール利用モードでは save_scene/edit_asset と同様に明示モードでのみ許可する
 * =====================================*/

using System;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPBridge.Editor.Tools
{
    public sealed class CreateTerrainTool : IMCPTool
    {
        private const int HeightmapResolution = 513;
        private const float DefaultSize = 1000f;
        private const float DefaultHeight = 600f;

        public string Name => "create_terrain";

        public string Description =>
            "新規TerrainDataアセットを作成し、それを使ったTerrainをシーンへ生成します(アセットファイルへの永続化を伴います)。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["assetPath"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "生成するTerrainDataアセットの保存先(例: Assets/Terrain/NewTerrain.asset)",
                },
                ["width"] = new JObject { ["type"] = "number", ["description"] = "地形サイズX(省略時1000)" },
                ["length"] = new JObject { ["type"] = "number", ["description"] = "地形サイズZ(省略時1000)" },
                ["height"] = new JObject { ["type"] = "number", ["description"] = "地形高さの最大値(省略時600)" },
                ["parentPath"] = new JObject { ["type"] = "string" },
            },
            ["required"] = new JArray("assetPath"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var assetPath = aArguments.Value<string>("assetPath");
                if (AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath) != null)
                {
                    throw new InvalidOperationException($"既にアセットが存在します: {assetPath}");
                }

                var width = aArguments["width"]?.Value<float>() ?? DefaultSize;
                var length = aArguments["length"]?.Value<float>() ?? DefaultSize;
                var height = aArguments["height"]?.Value<float>() ?? DefaultHeight;

                // TerrainDataはScriptableObjectではなく、newで直接生成する特殊なアセット型
                var terrainData = new TerrainData
                {
                    heightmapResolution = HeightmapResolution,
                    size = new Vector3(width, height, length),
                };

                AssetDatabase.CreateAsset(terrainData, assetPath);
                AssetDatabase.SaveAssets();

                var terrainGameObject = Terrain.CreateTerrainGameObject(terrainData);

                var parentPath = aArguments.Value<string>("parentPath");
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parent = MCPSceneObjectResolver.ResolveGameObject(parentPath).transform;
                    terrainGameObject.transform.SetParent(parent, false);
                }

                // 兄弟内で一意な名前になるようリネームする(MCPSceneObjectResolverはパス解決に名前を使うため)
                GameObjectUtility.EnsureUniqueNameForSibling(terrainGameObject);

                return new JObject
                {
                    ["objectPath"] = MCPSceneObjectResolver.GetHierarchyPath(terrainGameObject.transform),
                    ["assetPath"] = assetPath,
                };
            });
        }
    }
}
