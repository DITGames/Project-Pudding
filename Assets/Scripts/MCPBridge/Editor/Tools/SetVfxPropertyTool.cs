/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file SetVfxPropertyTool.cs
 * @author hqrse
 * @date 2026/08/21
 * @brief シーン上のVisualEffectコンポーネントのExposedプロパティ値を設定するMCPツール
 * GetVfxPropertyTool同様、シーン上のVisualEffectコンポーネントのランタイムAPI経由で
 * インスタンスのプロパティoverrideを扱う。SPEC.mdの合意により明示モードでのみ許可する
 * =====================================*/

using System;
using MCPBridge.Editor.Server;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.VFX;

namespace MCPBridge.Editor.Tools
{
    public sealed class SetVfxPropertyTool : IMCPTool
    {
        public string Name => "set_vfx_property";

        public string Description =>
            "objectPathで解決したVisualEffectコンポーネントのExposedプロパティ値(float/int/bool/vector2/vector3/vector4/color)を設定します。";

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
                ["value"] = new JObject(),
            },
            ["required"] = new JArray("objectPath", "propertyName", "propertyType", "value"),
        };

        public JToken Invoke(JObject aArguments)
        {
            return MCPMainThreadDispatcher.RunOnMainThread(() =>
            {
                var target = (VisualEffect)MCPSceneObjectResolver.ResolveComponentOrGameObject(
                    aArguments.Value<string>("objectPath"), "VisualEffect");

                var propertyName = aArguments.Value<string>("propertyName");
                var value = aArguments["value"];

                switch (aArguments.Value<string>("propertyType"))
                {
                    case "float":
                        RequireHas(target.HasFloat(propertyName), propertyName);
                        target.SetFloat(propertyName, value.Value<float>());
                        break;
                    case "int":
                        RequireHas(target.HasInt(propertyName), propertyName);
                        target.SetInt(propertyName, value.Value<int>());
                        break;
                    case "bool":
                        RequireHas(target.HasBool(propertyName), propertyName);
                        target.SetBool(propertyName, value.Value<bool>());
                        break;
                    case "vector2":
                        RequireHas(target.HasVector2(propertyName), propertyName);
                        target.SetVector2(propertyName, (Vector2)MCPArgumentConverter.ConvertValue(value, typeof(Vector2)));
                        break;
                    case "vector3":
                        RequireHas(target.HasVector3(propertyName), propertyName);
                        target.SetVector3(propertyName, (Vector3)MCPArgumentConverter.ConvertValue(value, typeof(Vector3)));
                        break;
                    case "vector4":
                        RequireHas(target.HasVector4(propertyName), propertyName);
                        target.SetVector4(propertyName, (Vector4)MCPArgumentConverter.ConvertValue(value, typeof(Vector4)));
                        break;
                    case "color":
                        RequireHas(target.HasVector4(propertyName), propertyName);
                        target.SetVector4(propertyName, (Color)MCPArgumentConverter.ConvertValue(value, typeof(Color)));
                        break;
                    default:
                        throw new ArgumentException($"未対応のpropertyTypeです: {aArguments.Value<string>("propertyType")}");
                }

                return new JObject { ["objectPath"] = aArguments.Value<string>("objectPath"), ["propertyName"] = propertyName };
            });
        }

        // VisualEffectはHas〜がfalseでもSet〜が黙って無視するため、見つからない場合は明示的にエラーにする
        private static void RequireHas(bool aHasProperty, string aPropertyName)
        {
            if (!aHasProperty)
            {
                throw new MCPToolException(-32008, $"VisualEffectにプロパティが見つかりません: {aPropertyName}");
            }
        }
    }
}
