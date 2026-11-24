/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPGameSettings.cs
 * @author hqrse
 * @date 2026/11/23
 * @brief 設定画面で変えるサウンドの設定の保存
 * =====================================*/

using System;
using UnityEngine;

namespace PPCore
{
    // 設定画面のサウンドで変える音量の種類
    public enum PPVolumeKind
    {
        Master = 0,
        Bgm = 1,
        Se = 2,
    }

    // 設定画面で変えるサウンドの設定。PlayerPrefs に保存し、最初に読んだときに読み込む
    // 全体の音量は AudioListener の音量へ反映する。BGM と SE は音を鳴らす仕組みができたらそちらで使う
    public static class PPGameSettings
    {
        /* 定数 */

        public const int VolumeMax = 10;
        public const int DefaultVolume = 8;
        private const string VolumeKeyPrefix = "PP.Settings.Volume.";

        /* 状態 */

        private static bool sIsLoaded;
        private static readonly int[] sVolumes = new int[3];

        /* イベント */

        // 設定のどれかが変わったとき
        public static event Action OnChanged;

        /* 問い合わせ */

        // 0〜VolumeMax
        public static int GetVolume(PPVolumeKind aKind)
        {
            EnsureLoaded();
            return sVolumes[(int)aKind];
        }

        /* 操作 */

        public static void SetVolume(PPVolumeKind aKind, int aVolume)
        {
            EnsureLoaded();
            int volume = Mathf.Clamp(aVolume, 0, VolumeMax);
            if (sVolumes[(int)aKind] == volume) return;
            sVolumes[(int)aKind] = volume;
            PlayerPrefs.SetInt(VolumeKeyPrefix + aKind, volume);
            PlayerPrefs.Save();
            ApplyVolume();
            OnChanged?.Invoke();
        }

        public static void ResetSound()
        {
            EnsureLoaded();
            foreach (PPVolumeKind kind in Enum.GetValues(typeof(PPVolumeKind)))
            {
                sVolumes[(int)kind] = DefaultVolume;
                PlayerPrefs.DeleteKey(VolumeKeyPrefix + kind);
            }
            PlayerPrefs.Save();
            ApplyVolume();
            OnChanged?.Invoke();
        }

        /* ヘルパー */

        private static void EnsureLoaded()
        {
            if (sIsLoaded) return;
            sIsLoaded = true;
            foreach (PPVolumeKind kind in Enum.GetValues(typeof(PPVolumeKind)))
            {
                sVolumes[(int)kind] = Mathf.Clamp(PlayerPrefs.GetInt(VolumeKeyPrefix + kind, DefaultVolume), 0, VolumeMax);
            }
            ApplyVolume();
        }

        private static void ApplyVolume() => AudioListener.volume = (float)sVolumes[(int)PPVolumeKind.Master] / VolumeMax;

        // シーンの読み込み前に読み込み、全体の音量を反映する
        // ドメインの再読み込みを切っているため、前回の Play の購読が残らないよう、読み込み直す前に消す
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnStartup()
        {
            OnChanged = null;
            sIsLoaded = false;
            EnsureLoaded();
        }
    }
}
