/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPConditionAssetFactory.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief 条件クラスの型から新規アセットを生成・保存する
 * =====================================*/
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    public static class PPConditionAssetFactory
    {
        private const string DefaultFolder = "Assets/GameData/AI/Conditions";

        public static PPPartyConditionValidator CreateAndSave(Type aType)
        {
            if(aType == null || aType.IsAbstract || !typeof(PPPartyConditionValidator).IsAssignableFrom(aType))
                return null;
            
            var menu = aType.GetCustomAttribute<PPConditionMenuAttribute>();
            var folderPath = DefaultFolder;
            if (menu != null)
            {
                folderPath += $"/{menu.FolderPath}";
            }
            
            EnsureFolder(folderPath);
            
            var instance = ScriptableObject.CreateInstance(aType) as PPPartyConditionValidator;
            string niceName = ObjectNames.NicifyVariableName(aType.Name);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{niceName}.asset");
            
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(instance);
            
            return instance;
        }

        public static void EnsureFolder(string aPath)
        {
            if(AssetDatabase.IsValidFolder(aPath)) return;
            
            string[] parts = aPath.Split('/');
            string current = parts[0];
            for (int i = 0; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}