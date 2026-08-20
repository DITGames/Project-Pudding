/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file GetMaterialPropertyTool.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief マテリアルのシェーダープロパティ値を読み取るMCPツール
 * マテリアルのシェーダープロパティはm_SavedProperties(内部配列表現)に格納されており
 * SerializedProperty経由での汎用アクセスに不向きなため、Materialの公開ランタイムAPIを直接使う。
 * 読み取り専用のためDebugモードでも許可する
 * =====================================*/

using System;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPBridge.Editor.Tools
{
    public sealed class GetMaterialPropertyTool : IMCPTool
    {
        public string Name => "get_material_property";

        public string Description =>
            "assetPathで指定したマテリアルのシェーダープロパティ値(float/color/vector/texture)を読み取ります。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["assetPath"] = new JObject { ["type"] = "string" },
                ["propertyName"] = new JObject { ["type"] = "string" },
                ["propertyType"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray("float", "color", "vector", "texture"),
                },
            },
            ["required"] = new JArray("assetPath", "propertyName", "propertyType"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var assetPath = aArguments.Value<string>("assetPath");
                var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (material == null)
                {
                    throw new InvalidOperationException($"マテリアルが見つかりません: {assetPath}");
                }

                var propertyName = aArguments.Value<string>("propertyName");
                if (!material.HasProperty(propertyName))
                {
                    throw new MCPToolException(-32006, $"マテリアルにプロパティが見つかりません: {propertyName}");
                }

                var propertyType = aArguments.Value<string>("propertyType");
                JToken value;
                if (propertyType == "texture")
                {
                    var texture = material.GetTexture(propertyName);
                    value = texture != null ? AssetDatabase.GetAssetPath(texture) : null;
                }
                else
                {
                    value = propertyType switch
                    {
                        "float" => material.GetFloat(propertyName),
                        "color" => MCPArgumentConverter.ToJToken(material.GetColor(propertyName)),
                        "vector" => MCPArgumentConverter.ToJToken(material.GetVector(propertyName)),
                        _ => throw new ArgumentException($"未対応のpropertyTypeです: {propertyType}"),
                    };
                }

                return new JObject { ["propertyName"] = propertyName, ["value"] = value };
            });
        }
    }
}
