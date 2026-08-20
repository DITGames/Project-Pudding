/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file SetAssetImportSettingsTool.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief TextureImporterのインポート設定を変更するMCPツール
 * インポート設定の変更・再インポート(ディスクへの永続化を伴う操作)であるため、
 * ツール利用モードでは明示モードでのみ許可する
 * =====================================*/

using System;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPBridge.Editor.Tools
{
    public sealed class SetAssetImportSettingsTool : IMCPTool
    {
        public string Name => "set_asset_import_settings";

        public string Description =>
            "assetPathで指定したテクスチャアセットのインポート設定(TextureImporter)を変更し、再インポートします(ディスクへの永続化を伴います)。指定したキーのみ上書きします。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["assetPath"] = new JObject { ["type"] = "string" },
                ["textureType"] = new JObject { ["type"] = "string" },
                ["maxTextureSize"] = new JObject { ["type"] = "integer" },
                ["filterMode"] = new JObject { ["type"] = "string" },
                ["wrapMode"] = new JObject { ["type"] = "string" },
                ["isReadable"] = new JObject { ["type"] = "boolean" },
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

                if (aArguments["textureType"] != null)
                {
                    importer.textureType = ParseEnum<TextureImporterType>(aArguments.Value<string>("textureType"));
                }
                if (aArguments["maxTextureSize"] != null)
                {
                    importer.maxTextureSize = aArguments.Value<int>("maxTextureSize");
                }
                if (aArguments["filterMode"] != null)
                {
                    importer.filterMode = ParseEnum<FilterMode>(aArguments.Value<string>("filterMode"));
                }
                if (aArguments["wrapMode"] != null)
                {
                    importer.wrapMode = ParseEnum<TextureWrapMode>(aArguments.Value<string>("wrapMode"));
                }
                if (aArguments["isReadable"] != null)
                {
                    importer.isReadable = aArguments.Value<bool>("isReadable");
                }

                importer.SaveAndReimport();
                return new JObject { ["assetPath"] = assetPath };
            });
        }

        // 文字列(大文字小文字を区別しない)から列挙値を解決する。未知の値はMCPToolExceptionにする
        private static T ParseEnum<T>(string aValue) where T : struct, Enum
        {
            if (Enum.TryParse<T>(aValue, true, out var result))
            {
                return result;
            }
            throw new MCPToolException(-32602, $"列挙子が見つかりません: {aValue} (有効な値: {string.Join(", ", Enum.GetNames(typeof(T)))})");
        }
    }
}
