/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file GetAssetImportSettingsTool.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief TextureImporterのインポート設定を読み取るMCPツール
 * 読み取り専用のためDebugモードでも許可する。書き込みはSetAssetImportSettingsToolが担う
 * =====================================*/

using System;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MCPBridge.Editor.Tools
{
    public sealed class GetAssetImportSettingsTool : IMCPTool
    {
        public string Name => "get_asset_import_settings";

        public string Description =>
            "assetPathで指定したテクスチャアセットのインポート設定(TextureImporter)を読み取ります。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["assetPath"] = new JObject { ["type"] = "string" },
            },
            ["required"] = new JArray("assetPath"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var assetPath = aArguments.Value<string>("assetPath");
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException($"TextureImporterではありません: {assetPath}");
                }

                return new JObject
                {
                    ["textureType"] = importer.textureType.ToString(),
                    ["maxTextureSize"] = importer.maxTextureSize,
                    ["filterMode"] = importer.filterMode.ToString(),
                    ["wrapMode"] = importer.wrapMode.ToString(),
                    ["isReadable"] = importer.isReadable,
                };
            });
        }
    }
}
