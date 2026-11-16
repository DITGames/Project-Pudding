/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPRuntimeSettingsValidator.cs
 * @author hqrse
 * @date 2026/10/22
 * @brief PPRuntimeSettingsアセットの配置をPlay前とビルド前に検査する
 * =====================================*/

using System.Collections.Generic;
using System.Text;
using CustomConsole;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace PPCore
{
    // PPRuntimeSettings アセットが正しい場所に 1 つだけあるかを検査する
    // Play 開始時はエラーログを出し、ビルド前は BuildFailedException でビルドを止める
    [InitializeOnLoad]
    public sealed class PPRuntimeSettingsValidator : IPreprocessBuildWithReport
    {
        // ビルド前処理の実行順。ほかの前処理との順序の指定は特にない
        public int callbackOrder => 0;

        static PPRuntimeSettingsValidator()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        // ビルドの直前に検査し、問題があればビルドを止める
        // aReport : ビルドの情報
        public void OnPreprocessBuild(BuildReport aReport)
        {
            if (!Validate(out var message))
            {
                throw new BuildFailedException($"[{PPRuntimeSettings.LogTag}] {message}");
            }
        }

        // Play に入った直後に検査し、問題があればエラーログを出す
        // Clear on Play に消されないよう、ExitingEditMode ではなく EnteredPlayMode で検査する
        private static void OnPlayModeChanged(PlayModeStateChange aState)
        {
            if (aState != PlayModeStateChange.EnteredPlayMode) return;

            if (!Validate(out var message))
            {
                CustomConsoleLog.Error(PPRuntimeSettings.LogTag, message);
            }
        }

        // アセットが正しい場所に 1 つだけあるかを検査する
        // 0 件、2 件以上、または PPRuntimeSettings.AssetPath 以外にある場合を問題とする
        public static bool Validate(out string aMessage)
        {
            var paths = FindAssetPaths();

            if (paths.Count == 0)
            {
                aMessage = $"PPRuntimeSettings アセットが見つかりません。{PPRuntimeSettings.AssetPath} に作成してください" +
                           "（Create > Project-Pudding/Settings/PPRuntimeSettings）";
                return false;
            }

            if (paths.Count > 1)
            {
                aMessage = $"PPRuntimeSettings アセットが {paths.Count} 個あります。{PPRuntimeSettings.AssetPath} の 1 つだけにしてください" +
                           FormatPaths(paths);
                return false;
            }

            if (paths[0] != PPRuntimeSettings.AssetPath)
            {
                aMessage = $"PPRuntimeSettings アセットの配置先が違います。{PPRuntimeSettings.AssetPath} に移動してください" +
                           FormatPaths(paths);
                return false;
            }

            aMessage = null;
            return true;
        }

        // PPRuntimeSettings アセットを選択して、プロジェクトウィンドウ上で示す
        // 正しい配置先に無ければ、見つかった最初のものを選ぶ
        [MenuItem("Window/Project-Pudding/PPRuntimeSettings を選択")]
        private static void SelectSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PPRuntimeSettings>(PPRuntimeSettings.AssetPath);
            if (settings == null)
            {
                var paths = FindAssetPaths();
                if (paths.Count > 0)
                {
                    settings = AssetDatabase.LoadAssetAtPath<PPRuntimeSettings>(paths[0]);
                }
            }

            if (settings == null)
            {
                CustomConsoleLog.Error(PPRuntimeSettings.LogTag,
                    $"PPRuntimeSettings アセットが見つかりません。{PPRuntimeSettings.AssetPath} に作成してください");
                return;
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        // プロジェクト内の PPRuntimeSettings アセットのパスをすべて集める
        // return : 見つかったアセットのパス
        private static List<string> FindAssetPaths()
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(PPRuntimeSettings)}"))
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            return paths;
        }

        // 見つかったパスをメッセージに付ける形に並べる
        private static string FormatPaths(List<string> aPaths)
        {
            var builder = new StringBuilder();
            builder.Append("\n見つかったアセット :");
            foreach (var path in aPaths)
            {
                builder.Append("\n  ").Append(path);
            }
            return builder.ToString();
        }
    }
}
