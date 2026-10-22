/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPRuntimeSettings.cs
 * @author hqrse
 * @date 2026/10/22
 * @brief 起動時に読み込む基底データの置き場所
 * =====================================*/

using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // 起動時に読み込む基底データ（ゲーム全体で共有する既定値の定義アセット）への参照をまとめるアセット
    //
    // シーンへの置き忘れに左右されないよう、シーン読み込み前に Resources から自動で読み込む
    // アセットは Assets/GameData/Settings/Resources/PPRuntimeSettings.asset に 1 つだけ置く
    // 見つからない場合はエラーを一度だけ出し、空の代替インスタンスで Play を続ける（ビルドはエディタ側の検査で止める）
    // 利用側は PPRuntimeSettings.Current から読む。テストなどで差し替えたいときは SetOverride を使う
    //
    // 型の取り違えを実行時まで持ち越さないよう、汎用リストではなく型付きのフィールドを 1 つずつ足していく
    // 既定値を追加する手順
    //   1. 既定値を持つ定義アセットの型を作る
    //   2. ここに [SerializeField] private + [Label] のフィールドと、読み取り専用のプロパティを足す（[Label] には using AttributeUtility が要る）
    //        例 : [Label("ユニットAIの既定")] [SerializeField] private PPUnitAIDefaultsDefinition mUnitAIDefaults;
    //             public PPUnitAIDefaultsDefinition UnitAIDefaults => mUnitAIDefaults;
    //   3. PPRuntimeSettings.asset のインスペクタで定義アセットを割り当てる
    //   4. 利用側は PPRuntimeSettings.Current.Xxx を読む。代替インスタンスや未割り当てでは null になるため、null を許容する
    // 実行中にアセットの値を書き換えないよう、公開するのは読み取り専用のプロパティだけにする
    [CreateAssetMenu(fileName = "PPRuntimeSettings", menuName = "Project-Pudding/Settings/PPRuntimeSettings")]
    public sealed class PPRuntimeSettings : ScriptableObject
    {
        // Resources.Load に渡すパス
        public const string ResourcesPath = "PPRuntimeSettings";
        // アセットの正しい配置先。エディタ側の検査で使う
        public const string AssetPath = "Assets/GameData/Settings/Resources/PPRuntimeSettings.asset";
        // ログのタグ
        public const string LogTag = "Settings";

        // Resources から読み込んだアセット。見つからなかった場合は代替インスタンス
        private static PPRuntimeSettings sLoaded;
        // SetOverride で差し替えられたアセット。null なら差し替えなし
        private static PPRuntimeSettings sOverride;
        // アセットが見つからないエラーを報告済みか。同じエラーを何度も出さないために持つ
        private static bool sHasReportedMissing;

        // 現在有効な設定
        // 優先順は 差し替え → 読み込み済み → その場で読み込み（見つからなければ代替）
        // 代替インスタンスを返すことはあっても null は返さない
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

        // アセットが見つからなかったときに作った代替インスタンスか
        public bool IsFallback { get; private set; }

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

        // static の状態を初期化する
        // Domain Reload を無効にしていると static が Play をまたいで残るため、前回の Play の値を持ち越さないようにする
        // 前回の代替インスタンスはここで破棄する（参照を外すだけだと Play のたびに残り続ける）
        // 本物のアセットは IsFallback が false なので破棄しない
        // Destroy はプレイモード中でないとエラーになり、破棄もフレーム末まで遅れるため、
        // Play 開始前のこの時点でも確実に動く DestroyImmediate を使う（対象はメモリ上だけのインスタンスなのでアセットは壊さない）
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
        // return : 読み込んだ設定、または代替インスタンス
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
