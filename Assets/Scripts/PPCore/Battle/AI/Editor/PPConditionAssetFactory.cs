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
    // 条件クラスの型から条件アセットを生成して保存するエディタ用ファクトリ
    // 保存先は PPConditionMenuAttribute の FolderPath に従って自動で決まるため、
    // 条件アセットが種類ごとに整理された状態を保てる
    // フォルダが無ければ階層ごと作成する
    public static class PPConditionAssetFactory
    {
        // 条件アセットを保存するルートフォルダ
        private const string DefaultFolder = "Assets/GameData/AI/Conditions";

        // 指定された条件型のアセットを生成して保存し、Project ウィンドウで位置を示す
        // 抽象型や条件クラス以外が渡された場合は何もしない
        // aType : 生成する条件クラスの型
        // return : 生成されたアセット。生成できなければ null
        public static PPPartyConditionValidator CreateAndSave(Type aType)
        {
            if(aType == null || aType.IsAbstract || !typeof(PPPartyConditionValidator).IsAssignableFrom(aType))
                return null;

            // 属性が付いていればその分だけサブフォルダを掘る
            var menu = aType.GetCustomAttribute<PPConditionMenuAttribute>();
            var folderPath = DefaultFolder;
            if (menu != null)
            {
                folderPath += $"/{menu.FolderPath}";
            }

            EnsureFolder(folderPath);

            var instance = ScriptableObject.CreateInstance(aType) as PPPartyConditionValidator;
            string niceName = ObjectNames.NicifyVariableName(aType.Name);
            // 同名アセットがあっても上書きしないよう連番付きのパスを取る
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{niceName}.asset");

            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(instance);

            return instance;
        }

        // 指定パスのフォルダが存在することを保証する
        // AssetDatabase.CreateFolder は 1 階層ずつしか作れないため、
        // パスを分解して root から順に掘っていく
        // aPath : 作成したいフォルダパス（Assets からの相対）
        public static void EnsureFolder(string aPath)
        {
            if(AssetDatabase.IsValidFolder(aPath)) return;

            string[] parts = aPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
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
