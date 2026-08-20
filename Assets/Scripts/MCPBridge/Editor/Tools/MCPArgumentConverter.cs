/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPArgumentConverter.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief JTokenとCLR値(メソッド引数・戻り値・SerializedProperty)の相互変換
 * Unity固有型の読み書きはMCPUnityJsonSerializerへ委譲し、
 * 書き出しと読み込みの表現が食い違わないようにしている
 * =====================================*/

using System;
using System.Reflection;
using MCPBridge.Editor.Logging;
using MCPBridge.Editor.Server;
using MCPBridge.Editor.Tools.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPBridge.Editor.Tools
{
    public static class MCPArgumentConverter
    {
        private const string LogTag = "MCPBridge";

        // JSON-RPCの不正パラメータを表すエラーコード
        private const int InvalidParamsCode = -32602;

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
        public static object ConvertValue(JToken aToken, Type aTargetType)
        {
            if (aToken == null || aToken.Type == JTokenType.Null)
            {
                return aTargetType.IsValueType ? Activator.CreateInstance(aTargetType) : null;
            }

            // MCPクライアント側の実装により、オブジェクト型の引数がJSON文字列として
            // 二重エンコードされて届く場合があるため、対象型がプリミティブ/文字列以外で
            // 受け取った値がJSONオブジェクト/配列らしき文字列だった場合は再パースする。
            // ただしUnityEngine.Object派生はアセットパス文字列を正規の表現として扱うため対象外
            if (aToken.Type == JTokenType.String &&
                !aTargetType.IsPrimitive &&
                aTargetType != typeof(string) &&
                !typeof(UnityEngine.Object).IsAssignableFrom(aTargetType))
            {
                var text = aToken.Value<string>()?.TrimStart();
                if (!string.IsNullOrEmpty(text) && (text[0] == '{' || text[0] == '['))
                {
                    try
                    {
                        aToken = JToken.Parse(text);
                    }
                    catch (JsonReaderException e)
                    {
                        throw new MCPToolException(InvalidParamsCode, $"値をJSONとして解釈できません: {e.Message}");
                    }
                }
            }

            if (aTargetType.IsEnum)
            {
                return ParseEnum(aToken, aTargetType);
            }

            try
            {
                return aToken.ToObject(aTargetType, MCPUnityJsonSerializer.Instance);
            }
            catch (JsonException e)
            {
                throw new MCPToolException(InvalidParamsCode,
                    $"値を{aTargetType.Name}へ変換できません: {e.Message}");
            }
        }

        // CLRの戻り値をJTokenへ変換する
        public static JToken ToJToken(object aValue)
        {
            if (aValue == null)
            {
                return JValue.CreateNull();
            }

            try
            {
                return JToken.FromObject(aValue, MCPUnityJsonSerializer.Instance);
            }
            catch (Exception e)
            {
                // 既知の型は専用コンバーター、未知の型はフィールド限定シリアライズで大半は防げるが、
                // それでも変換できない型はMCPクライアントへ例外を返さず文字列にフォールバックする
                MCPLog.Warning(LogTag, $"値のJSON変換に失敗したため文字列として返します: {e.Message}");
                return new JValue(aValue.ToString());
            }
        }

        public static Vector2 ReadVector2(JToken aToken)
        {
            return (Vector2)ConvertValue(aToken, typeof(Vector2));
        }

        public static Vector3 ReadVector3(JToken aToken)
        {
            return (Vector3)ConvertValue(aToken, typeof(Vector3));
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
                    ApplyEnum(aProperty, aValue);
                    break;
                case SerializedPropertyType.Vector2:
                    aProperty.vector2Value = ReadVector2(aValue);
                    break;
                case SerializedPropertyType.Vector3:
                    aProperty.vector3Value = ReadVector3(aValue);
                    break;
                case SerializedPropertyType.ObjectReference:
                    aProperty.objectReferenceValue =
                        MCPUnityObjectResolver.Resolve(aValue, ResolveObjectReferenceType(aProperty));
                    break;
                default:
                    throw new MCPToolException(InvalidParamsCode,
                        $"この型はMCP経由で設定できません: {aProperty.propertyType} ({aProperty.propertyPath})");
            }
        }

        // SerializedPropertyはオブジェクト参照の期待型を直接公開しないが、typeプロパティが
        // "PPtr<型名>"(スクリプト由来の型は"PPtr<$型名>")形式を返すためここから復元する。
        // 復元できた型をResolverへ渡すことで、Transform/Componentの型救済と型不一致の警告が働く
        private static Type ResolveObjectReferenceType(SerializedProperty aProperty)
        {
            var raw = aProperty.type;
            if (!raw.StartsWith("PPtr<", StringComparison.Ordinal) || !raw.EndsWith(">", StringComparison.Ordinal))
            {
                return typeof(UnityEngine.Object);
            }

            var typeName = raw["PPtr<".Length..^1].TrimStart('$');
            foreach (var candidate in TypeCache.GetTypesDerivedFrom<UnityEngine.Object>())
            {
                // 同名の型が複数の名前空間に存在しうるが、判別材料が型名しか無いため最初の一致を採る。
                // 取り違えた場合もResolver側の型不一致チェックで弾かれ、黙って誤った参照は入らない
                if (candidate.Name == typeName)
                {
                    return candidate;
                }
            }

            return typeof(UnityEngine.Object);
        }

        // 数値は列挙値そのもの、文字列は列挙子名(大文字小文字を区別しない)として解釈する
        private static object ParseEnum(JToken aToken, Type aTargetType)
        {
            if (aToken.Type == JTokenType.Integer)
            {
                return Enum.ToObject(aTargetType, aToken.Value<long>());
            }

            var name = aToken.Value<string>();
            try
            {
                return Enum.Parse(aTargetType, name, true);
            }
            catch (Exception)
            {
                throw new MCPToolException(InvalidParamsCode,
                    $"列挙子が見つかりません: {name} (有効な値: {string.Join(", ", Enum.GetNames(aTargetType))})");
            }
        }

        // 数値は列挙値そのもの、文字列は列挙子名として書き込む。
        // enumValueFlagは列挙値を直接扱うため[Flags]の複合値も保持できる。
        // 一方enumValueIndexは「i番目の列挙子を選ぶ」意味なので、enumNamesから引いたindexを
        // 渡せばUnity側が正しい列挙値へ変換してくれる(列挙型の実型を復元する必要がない)
        private static void ApplyEnum(SerializedProperty aProperty, JToken aValue)
        {
            if (aValue.Type == JTokenType.Integer)
            {
                aProperty.enumValueFlag = aValue.Value<int>();
                return;
            }

            var name = aValue.Value<string>();
            var names = aProperty.enumNames;
            for (var i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    aProperty.enumValueIndex = i;
                    return;
                }
            }

            throw new MCPToolException(InvalidParamsCode,
                $"列挙子が見つかりません: {name} (有効な値: {string.Join(", ", names)})");
        }
    }
}
