/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPUnityObjectResolver.cs
 * @author hqrse
 * @date 2026/08/20
 * @brief JSON表現からUnityEngine.Objectを解決する共通処理
 * MCPUnityObjectConverter(get_field/set_field)とApplyToSerializedProperty(edit_asset)の
 * 双方から使い、オブジェクト参照の解決規則を1箇所に集約する
 * =====================================*/

using System;
using MCPBridge.Editor.Logging;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPBridge.Editor.Tools.Serialization
{
    internal static class MCPUnityObjectResolver
    {
        private const string LogTag = "MCPBridge";

        // JSON表現(アセットパス文字列/GUID文字列/{instanceID}/{path})からオブジェクトを解決する
        // aExpectedType: 期待する型。Transform/Component派生については型救済を行う
        // 解決できない場合はnullを返し、理由を警告ログに残す
        public static UnityEngine.Object Resolve(JToken aToken, Type aExpectedType)
        {
            if (aToken == null || aToken.Type == JTokenType.Null)
            {
                return null;
            }

            var type = aExpectedType ?? typeof(UnityEngine.Object);

            if (aToken.Type == JTokenType.String)
            {
                return ResolveFromString(aToken.Value<string>(), type);
            }

            if (aToken is JObject obj)
            {
                if (obj["instanceID"] != null)
                {
                    return ResolveFromInstanceId(obj, type);
                }
                if (obj["guid"] != null)
                {
                    return ResolveFromString(obj["guid"].Value<string>(), type);
                }
                if (obj["path"] != null)
                {
                    return ResolveFromString(obj["path"].Value<string>(), type);
                }

                MCPLog.Warning(LogTag,
                    $"オブジェクト参照を解決できません。instanceID/guid/pathのいずれかが必要です: {obj.ToString(Newtonsoft.Json.Formatting.None)}");
                return null;
            }

            MCPLog.Warning(LogTag, $"オブジェクト参照として解釈できない値です: {aToken}");
            return null;
        }

        private static UnityEngine.Object ResolveFromString(string aValue, Type aExpectedType)
        {
            if (string.IsNullOrEmpty(aValue))
            {
                return null;
            }

            // GUID形式ならアセットパスへ変換してから読み込む
            if (IsGuid(aValue))
            {
                var guidPath = AssetDatabase.GUIDToAssetPath(aValue.Replace("-", string.Empty).ToLowerInvariant());
                if (string.IsNullOrEmpty(guidPath))
                {
                    MCPLog.Warning(LogTag, $"GUIDに対応するアセットが見つかりません: {aValue}");
                    return null;
                }
                return LoadAsset(guidPath, aExpectedType);
            }

            return LoadAsset(aValue, aExpectedType);
        }

        private static UnityEngine.Object LoadAsset(string aPath, Type aExpectedType)
        {
            var asset = AssetDatabase.LoadAssetAtPath(aPath, aExpectedType);
            if (asset != null)
            {
                return asset;
            }

            // 期待型で読めない場合でも、型救済が効く可能性があるためObjectとして読み直す
            var fallback = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(aPath);
            if (fallback == null)
            {
                MCPLog.Warning(LogTag, $"アセットを読み込めません: {aPath} (期待する型: {aExpectedType.Name})");
                return null;
            }

            return Adapt(fallback, aExpectedType, aPath);
        }

        private static UnityEngine.Object ResolveFromInstanceId(JObject aObject, Type aExpectedType)
        {
            var instanceId = aObject["instanceID"].Value<int>();
            var resolved = EditorUtility.InstanceIDToObject(instanceId);
            if (resolved == null)
            {
                var name = aObject["name"]?.Value<string>() ?? "(不明)";
                MCPLog.Warning(LogTag,
                    $"instanceIDを解決できません: {instanceId} (name: {name})。オブジェクトが破棄されたか、IDが古い可能性があります");
                return null;
            }

            return Adapt(resolved, aExpectedType, $"instanceID {instanceId}");
        }

        // 解決されたオブジェクトの型が期待型と異なる場合に、GameObject⇔Component間の橋渡しを試みる
        private static UnityEngine.Object Adapt(UnityEngine.Object aResolved, Type aExpectedType, string aSourceLabel)
        {
            if (aExpectedType.IsInstanceOfType(aResolved))
            {
                return aResolved;
            }

            if (aResolved is GameObject gameObject)
            {
                if (aExpectedType == typeof(Transform))
                {
                    return gameObject.transform;
                }
                if (typeof(Component).IsAssignableFrom(aExpectedType))
                {
                    var component = gameObject.GetComponent(aExpectedType);
                    if (component != null)
                    {
                        return component;
                    }
                    MCPLog.Warning(LogTag,
                        $"GameObject '{gameObject.name}' に {aExpectedType.Name} コンポーネントがありません ({aSourceLabel})");
                    return null;
                }
            }

            // Componentが解決されたがGameObjectを期待している場合の逆方向
            if (aResolved is Component sourceComponent && aExpectedType == typeof(GameObject))
            {
                return sourceComponent.gameObject;
            }

            MCPLog.Warning(LogTag,
                $"型が一致しません: {aResolved.GetType().Name} を解決しましたが {aExpectedType.Name} が必要です ({aSourceLabel})");
            return null;
        }

        // ハイフン有無を問わず32桁の16進文字列ならGUIDとみなす
        private static bool IsGuid(string aValue)
        {
            var normalized = aValue.Replace("-", string.Empty);
            if (normalized.Length != 32)
            {
                return false;
            }

            foreach (var c in normalized)
            {
                var isHex = c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
                if (!isHex)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
