/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file GetVfxPropertyTool.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief シーン上のVisualEffectコンポーネントのExposedプロパティ値を読み取るMCPツール
 * .vfxアセット内部のシリアライズデータ(Exposedプロパティのデフォルト値)を直接読み書きする
 * 公開APIは存在しないため、シーン上のVisualEffectコンポーネントのランタイムAPI経由で
 * インスタンスのプロパティoverrideを扱う(PLAN.md「SPEC.mdからの実装方針の変更」参照)。
 * 読み取り専用のためDebugモードでも許可する
 * =====================================*/

using System;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.VFX;

namespace MCPBridge.Editor.Tools
{
    public sealed class GetVfxPropertyTool : IMCPTool
    {
        public string Name => "get_vfx_property";

        public string Description =>
            "objectPathで解決したVisualEffectコンポーネントのExposedプロパティ値(float/int/bool/vector2/vector3/vector4/color)を読み取ります。";

        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["objectPath"] = new JObject { ["type"] = "string" },
                ["propertyName"] = new JObject { ["type"] = "string" },
                ["propertyType"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray("float", "int", "bool", "vector2", "vector3", "vector4", "color"),
                },
            },
            ["required"] = new JArray("objectPath", "propertyName", "propertyType"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var target = (VisualEffect)MCPSceneObjectResolver.ResolveComponentOrGameObject(
                    aArguments.Value<string>("objectPath"), "VisualEffect");

                var propertyName = aArguments.Value<string>("propertyName");
                var propertyType = aArguments.Value<string>("propertyType");

                JToken value = propertyType switch
                {
                    "float" => target.HasFloat(propertyName) ? target.GetFloat(propertyName) : Missing(propertyName),
                    "int" => target.HasInt(propertyName) ? target.GetInt(propertyName) : Missing(propertyName),
                    "bool" => target.HasBool(propertyName) ? target.GetBool(propertyName) : Missing(propertyName),
                    "vector2" => target.HasVector2(propertyName)
                        ? MCPArgumentConverter.ToJToken(target.GetVector2(propertyName))
                        : Missing(propertyName),
                    "vector3" => target.HasVector3(propertyName)
                        ? MCPArgumentConverter.ToJToken(target.GetVector3(propertyName))
                        : Missing(propertyName),
                    "vector4" => target.HasVector4(propertyName)
                        ? MCPArgumentConverter.ToJToken(target.GetVector4(propertyName))
                        : Missing(propertyName),
                    "color" => target.HasVector4(propertyName)
                        ? MCPArgumentConverter.ToJToken((Color)(Vector4)target.GetVector4(propertyName))
                        : Missing(propertyName),
                    _ => throw new ArgumentException($"未対応のpropertyTypeです: {propertyType}"),
                };

                return new JObject { ["propertyName"] = propertyName, ["value"] = value };
            });
        }

        // VisualEffectはHas〜がfalseでもGet〜が既定値を返し得るため、見つからない場合は明示的にエラーにする
        private static JToken Missing(string aPropertyName)
        {
            throw new MCPToolException(-32008, $"VisualEffectにプロパティが見つかりません: {aPropertyName}");
        }
    }
}
