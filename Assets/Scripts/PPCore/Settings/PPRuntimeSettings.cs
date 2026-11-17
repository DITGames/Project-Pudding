/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPRuntimeSettings.cs
 * @author hqrse
 * @date 2026/10/22
 * @brief 起動時に読み込む基底データの置き場所
 * =====================================*/

using AttributeUtility;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // ゲーム全体で共有する既定値の定義アセットへの参照をまとめるアセット。Resources から自動で読み込み、PPRuntimeSettings.Current から読む
    // 既定値は型付きのフィールドと読み取り専用プロパティを 1 つずつ足して追加する
    [CreateAssetMenu(fileName = "PPRuntimeSettings", menuName = "Project-Pudding/Settings/PPRuntimeSettings")]
    public sealed class PPRuntimeSettings : ScriptableObject
    {
        /* 定数 */

        // Resources.Load に渡すパス
        public const string ResourcesPath = "PPRuntimeSettings";
        // アセットの正しい配置先。エディタ側の検査で使う
        public const string AssetPath = "Assets/GameData/Settings/Resources/PPRuntimeSettings.asset";
        // ログのタグ
        public const string LogTag = "Settings";

        /* static 変数 */

        // 現在有効な設定。優先順は 差し替え → 読み込み済み → その場で読み込み（見つからなければ代替）
        // null は返さない
        public static PPRuntimeSettings Current
        {
            get
            {
                if (sOverride != null) return sOverride;
                // UnityEngine.Object は破棄済みでも参照が残るため、?? ではなく == null で判定する
                if (sLoaded == null)
                {
                    sLoaded = Load();
                }
                return sLoaded;
            }
        }

        // Resources から読み込んだアセット。見つからなかった場合は代替インスタンス
        private static PPRuntimeSettings sLoaded;
        // SetOverride で差し替えられたアセット。null なら差し替えなし
        private static PPRuntimeSettings sOverride;
        // アセットが見つからないエラーを報告済みか。同じエラーを何度も出さないために持つ
        private static bool sHasReportedMissing;

        /* インスペクタ設定 */

        [Label("ユニットAIの既定")]
        [SerializeField] private PPUnitAIDefaultsDefinition mUnitAIDefaults;
        // 未設定なら、入力アクションのアセットに書かれた割り当てを既定にする
        [Label("キー設定の既定")]
        [SerializeField] private PPDefaultKeyConfigDefinition mDefaultKeyConfig;

        /* 状態 */

        // アセットが見つからなかったときに作った代替インスタンスか
        public bool IsFallback { get; private set; }

        /* プロパティ */

        public PPUnitAIDefaultsDefinition UnitAIDefaults => mUnitAIDefaults;
        public PPDefaultKeyConfigDefinition DefaultKeyConfig => mDefaultKeyConfig;

        /* static 関数 */

        // 現在の設定を差し替える。null を渡すと ClearOverride と同じ
        // aSettings : 差し替える設定
        public static void SetOverride(PPRuntimeSettings aSettings)
        {
            sOverride = aSettings;
        }

        // 差し替えを解除し、Resources から読み込んだ設定に戻す
        public static void ClearOverride()
        {
            sOverride = null;
        }

        /* ヘルパー */

        // static の状態を初期化する（Domain Reload 無効時に前回の Play の値を持ち越さないため）
        // 前回の代替インスタンスは DestroyImmediate で破棄する
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (sLoaded != null && sLoaded.IsFallback)
            {
                DestroyImmediate(sLoaded);
            }
            sLoaded = null;
            sOverride = null;
            sHasReportedMissing = false;
        }

        // シーン読み込み前に設定を読み込む
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnStartup()
        {
            if (sLoaded == null)
            {
                sLoaded = Load();
            }
        }

        // Resources から設定を読み込む
        // 見つからなければエラーを一度だけ出し、空の代替インスタンスを返す
        private static PPRuntimeSettings Load()
        {
            var settings = Resources.Load<PPRuntimeSettings>(ResourcesPath);
            if (settings != null)
            {
                CustomConsoleLog.Log(LogTag, $"PPRuntimeSettings を読み込みました (Resources/{ResourcesPath})", settings);
                return settings;
            }

            if (!sHasReportedMissing)
            {
                sHasReportedMissing = true;
                CustomConsoleLog.Error(LogTag,
                    $"PPRuntimeSettings が Resources/{ResourcesPath} に見つかりません。" +
                    $"{AssetPath} に作成してください。空の代替設定で続行します");
            }

            var fallback = CreateInstance<PPRuntimeSettings>();
            fallback.name = "PPRuntimeSettings (Fallback)";
            fallback.IsFallback = true;
            return fallback;
        }
    }
}
