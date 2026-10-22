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
    // PPRuntimeSettings アセットの配置を検査するエディタ側の仕組み
    //
    // ランタイム側はアセットが無くても代替インスタンスで Play を続けるため、置き忘れや置き間違いに気づきにくい
    // そこで Play に入った直後とビルドの直前に、アセットが正しい場所に 1 つだけあるかを確かめる
    //   - Play 開始時 : 問題があればエラーログを出す。Play は止めない
    //   - ビルド前 : 問題があれば BuildFailedException を投げてビルドを止める
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
        // ExitingEditMode で出すと、CustomConsoleLogStore の Clear on Play（同じ ExitingEditMode で走り、呼ばれる順は決まっていない）に消されることがある
        // ログの消去より後に出すため EnteredPlayMode で検査する。Domain Reload をまたいで結果を持ち越す必要もない
        // aState : 遷移後のプレイモード状態
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
        // aMessage : 問題があったときの説明。問題がなければ null
        // return : 問題がなければ true
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
        // aPaths : 並べるパス
        // return : 改行で区切ったパスの一覧
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
