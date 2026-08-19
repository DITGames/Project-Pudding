/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPSceneObjectResolver.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief 階層パス・コンポーネント型名からシーン上のオブジェクトを解決する共通処理
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MCPBridge.Editor.Tools
{
    public static class MCPSceneObjectResolver
    {
        // 階層パス(例: "Parent/Child/Grandchild")からアクティブシーン上のGameObjectを検索する
        public static GameObject ResolveGameObject(string aObjectPath)
        {
            if (string.IsNullOrEmpty(aObjectPath))
            {
                throw new ArgumentException("objectPathを指定してください。");
            }

            var segments = aObjectPath.Split('/');
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            var current = rootObjects.FirstOrDefault(r => r.name == segments[0]);
            if (current == null)
            {
                throw new InvalidOperationException($"GameObjectが見つかりません: {segments[0]}");
            }

            for (var i = 1; i < segments.Length; i++)
            {
                var child = current.transform.Find(segments[i]);
                if (child == null)
                {
                    throw new InvalidOperationException($"GameObjectが見つかりません: {aObjectPath}");
                }
                current = child.gameObject;
            }
            return current;
        }

        // objectPathで見つけたGameObjectから、componentTypeで指定した型のコンポーネントを取得する
        // componentTypeが未指定または"GameObject"の場合はGameObject自身を返す
        public static UnityEngine.Object ResolveComponentOrGameObject(string aObjectPath, string aComponentTypeName)
        {
            var go = ResolveGameObject(aObjectPath);
            if (string.IsNullOrEmpty(aComponentTypeName) || aComponentTypeName == "GameObject")
            {
                return go;
            }

            var type = ResolveType(aComponentTypeName);
            var component = go.GetComponent(type);
            if (component == null)
            {
                throw new InvalidOperationException($"コンポーネントが見つかりません: {aComponentTypeName} ({aObjectPath})");
            }
            return component;
        }

        // 単純型名または完全修飾名から、ロード済みアセンブリを走査してTypeを解決する
        public static Type ResolveType(string aTypeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in SafeGetTypes(assembly))
                {
                    if (type.Name == aTypeName || type.FullName == aTypeName)
                    {
                        return type;
                    }
                }
            }
            throw new InvalidOperationException($"型が見つかりません: {aTypeName}");
        }

        // Transformの階層パスを組み立てる(ResolveGameObjectの逆変換)
        public static string GetHierarchyPath(Transform aTransform)
        {
            var names = new List<string>();
            var current = aTransform;
            while (current != null)
            {
                names.Insert(0, current.name);
                current = current.parent;
            }
            return string.Join("/", names);
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly aAssembly)
        {
            try
            {
                return aAssembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }
    }
}
