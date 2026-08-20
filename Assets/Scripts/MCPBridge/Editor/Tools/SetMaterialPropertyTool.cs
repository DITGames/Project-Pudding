/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file SetMaterialPropertyTool.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief マテリアルへのShader割当・シェーダープロパティ値の設定を行うMCPツール
 * GetMaterialPropertyTool同様、Materialの公開ランタイムAPIを直接使う。
 * アセットファイルへの永続化を伴うため明示モードでのみ許可する
 * =====================================*/

using System;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPBridge.Editor.Tools
{
    public sealed class SetMaterialPropertyTool : IMCPTool
    {
        public string Name => "set_material_property";

        public string Description =>
            "assetPathで指定したマテリアルへShaderを割り当て、および/またはシェーダープロパティ値(float/color/vector/texture)を設定します(ディスクへの永続化を伴います)。shaderPath・propertyName+propertyType+valueはそれぞれ省略可能ですが、少なくとも一方は指定してください。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["assetPath"] = new JObject { ["type"] = "string" },
                ["shaderPath"] = new JObject { ["type"] = "string", ["description"] = "割り当てるShaderアセットのパス(省略可)" },
                ["propertyName"] = new JObject { ["type"] = "string" },
                ["propertyType"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray("float", "color", "vector", "texture"),
                },
                ["value"] = new JObject(),
            },
            ["required"] = new JArray("assetPath"),
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

                var shaderPath = aArguments.Value<string>("shaderPath");
                if (!string.IsNullOrEmpty(shaderPath))
                {
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                    if (shader == null)
                    {
                        throw new InvalidOperationException($"Shaderが見つかりません: {shaderPath}");
                    }
                    material.shader = shader;
                }

                var propertyName = aArguments.Value<string>("propertyName");
                if (!string.IsNullOrEmpty(propertyName))
                {
                    if (!material.HasProperty(propertyName))
                    {
                        throw new MCPToolException(-32006, $"マテリアルにプロパティが見つかりません: {propertyName}");
                    }

                    switch (aArguments.Value<string>("propertyType"))
                    {
                        case "float":
                            material.SetFloat(propertyName, aArguments.Value<float>("value"));
                            break;
                        case "color":
                            material.SetColor(propertyName,
                                (Color)MCPArgumentConverter.ConvertValue(aArguments["value"], typeof(Color)));
                            break;
                        case "vector":
                            material.SetVector(propertyName,
                                (Vector4)MCPArgumentConverter.ConvertValue(aArguments["value"], typeof(Vector4)));
                            break;
                        case "texture":
                            var texturePath = aArguments.Value<string>("value");
                            var texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
                            if (texture == null)
                            {
                                throw new InvalidOperationException($"テクスチャが見つかりません: {texturePath}");
                            }
                            material.SetTexture(propertyName, texture);
                            break;
                        default:
                            throw new ArgumentException($"未対応のpropertyTypeです: {aArguments.Value<string>("propertyType")}");
                    }
                }

                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
                return new JObject { ["assetPath"] = assetPath };
            });
        }
    }
}
