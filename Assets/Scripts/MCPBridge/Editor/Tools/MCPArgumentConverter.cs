/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPArgumentConverter.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief JTokenとCLR値(メソッド引数・戻り値・SerializedProperty)の相互変換
 * =====================================*/

using System;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPBridge.Editor.Tools
{
    public static class MCPArgumentConverter
    {
        // JArrayをMethodInfoの引数リストに合わせて変換する
        public static object[] Convert(JArray aArgs, MethodInfo aMethod)
        {
            var parameters = aMethod.GetParameters();
            var result = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var token = aArgs != null && i < aArgs.Count ? aArgs[i] : null;
                result[i] = ConvertValue(token, parameters[i].ParameterType);
            }
            return result;
        }

        // JTokenを指定型のCLR値に変換する
        // Vector2/Vector3はNewtonsoftの標準変換がUnityの公開フィールドに対応しないため専用処理を通す
        public static object ConvertValue(JToken aToken, Type aTargetType)
        {
            if (aToken == null || aToken.Type == JTokenType.Null)
            {
                return aTargetType.IsValueType ? Activator.CreateInstance(aTargetType) : null;
            }

            // MCPクライアント側の実装により、オブジェクト型の引数がJSON文字列として
            // 二重エンコードされて届く場合があるため、対象型がプリミティブ/文字列以外で
            // 受け取った値がJSONオブジェクト/配列らしき文字列だった場合は再パースする
            if (aToken.Type == JTokenType.String && !aTargetType.IsPrimitive && aTargetType != typeof(string))
            {
                var text = aToken.Value<string>()?.TrimStart();
                if (!string.IsNullOrEmpty(text) && (text[0] == '{' || text[0] == '['))
                {
                    aToken = JToken.Parse(text);
                }
            }

            if (aTargetType.IsEnum)
            {
                return Enum.Parse(aTargetType, aToken.Value<string>());
            }
            if (aTargetType == typeof(Vector2))
            {
                return ReadVector2(aToken);
            }
            if (aTargetType == typeof(Vector3))
            {
                return ReadVector3(aToken);
            }
            return aToken.ToObject(aTargetType);
        }

        // CLRの戻り値をJTokenへ変換する
        // Vector2/Vector3等はnormalized/magnitude等の再帰的なプロパティを持つため、
        // Newtonsoftの既定のリフレクションシリアライズだと自己参照ループで例外になる。
        // 該当する型は明示的にプリミティブなJSONへ変換してから返す
        public static JToken ToJToken(object aValue)
        {
            return aValue switch
            {
                null => JValue.CreateNull(),
                Vector2 v2 => new JObject { ["x"] = v2.x, ["y"] = v2.y },
                Vector3 v3 => new JObject { ["x"] = v3.x, ["y"] = v3.y, ["z"] = v3.z },
                Quaternion q => new JObject { ["x"] = q.x, ["y"] = q.y, ["z"] = q.z, ["w"] = q.w },
                _ => JToken.FromObject(aValue),
            };
        }

        public static Vector2 ReadVector2(JToken aToken)
        {
            return new Vector2(aToken["x"]?.Value<float>() ?? 0f, aToken["y"]?.Value<float>() ?? 0f);
        }

        public static Vector3 ReadVector3(JToken aToken)
        {
            return new Vector3(
                aToken["x"]?.Value<float>() ?? 0f,
                aToken["y"]?.Value<float>() ?? 0f,
                aToken["z"]?.Value<float>() ?? 0f);
        }

        // SerializedPropertyへJTokenの値を書き込む(edit_assetツール用)
        public static void ApplyToSerializedProperty(SerializedProperty aProperty, JToken aValue)
        {
            switch (aProperty.propertyType)
            {
                case SerializedPropertyType.Integer:
                    aProperty.intValue = aValue.Value<int>();
                    break;
                case SerializedPropertyType.Boolean:
                    aProperty.boolValue = aValue.Value<bool>();
                    break;
                case SerializedPropertyType.Float:
                    aProperty.floatValue = aValue.Value<float>();
                    break;
                case SerializedPropertyType.String:
                    aProperty.stringValue = aValue.Value<string>();
                    break;
                case SerializedPropertyType.Enum:
                    aProperty.enumValueIndex = aValue.Value<int>();
                    break;
                case SerializedPropertyType.Vector2:
                    aProperty.vector2Value = ReadVector2(aValue);
                    break;
                case SerializedPropertyType.Vector3:
                    aProperty.vector3Value = ReadVector3(aValue);
                    break;
                default:
                    throw new NotSupportedException($"未対応のプロパティ型です: {aProperty.propertyType}");
            }
        }
    }
}
