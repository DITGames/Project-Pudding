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
    /// <summary>
    /// 条件クラスの型から条件アセットを生成して保存するエディタ用ファクトリ。
    /// <para>
    /// 保存先は <see cref="PPConditionMenuAttribute"/> の FolderPath に従って自動で決まるため、
    /// 条件アセットが種類ごとに整理された状態を保てる。
    /// フォルダが無ければ階層ごと作成する。
    /// </para>
    /// </summary>
    public static class PPConditionAssetFactory
    {
        /// <summary>条件アセットを保存するルートフォルダ。</summary>
        private const string DefaultFolder = "Assets/GameData/AI/Conditions";

        /// <summary>
        /// 指定された条件型のアセットを生成して保存し、Project ウィンドウで位置を示す。
        /// 抽象型や条件クラス以外が渡された場合は何もしない。
        /// </summary>
        /// <param name="aType">生成する条件クラスの型。</param>
        /// <returns>生成されたアセット。生成できなければ null。</returns>
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

        /// <summary>
        /// 指定パスのフォルダが存在することを保証する。
        /// <see cref="AssetDatabase.CreateFolder"/> は 1 階層ずつしか作れないため、
        /// パスを分解して root から順に掘っていく。
        /// </summary>
        /// <param name="aPath">作成したいフォルダパス（Assets からの相対）。</param>
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
