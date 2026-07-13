/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file RecentAssetsHistory.cs
 * @author hqrse
 * @date 2026/07/10
 * @brief 最近開いたアセットの履歴を保持・永続化するクラス
 * EditorPrefsにJSON形式で保存し、Unityエディタの再起動をまたいで履歴を保持する
 * =====================================*/
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RecentAssetsWindow.Editor
{
    public static class RecentAssetsHistory
    {
        private const int MaxEntryCount = 200;
        private static readonly string sPrefsKey = "RecentAssetsWindow.History." + PlayerSettings.productGUID;

        private static List<RecentAssetEntry> sEntries;

        public static event Action OnChanged;

        private static List<RecentAssetEntry> Entries
        {
            get
            {
                if (sEntries == null)
                {
                    Load();
                }
                return sEntries;
            }
        }

        // 指定GUIDのアセットを履歴の先頭に追加する。既に存在する場合は先頭へ移動する
        public static void Add(string aGuid)
        {
            if (string.IsNullOrEmpty(aGuid))
            {
                return;
            }

            Entries.RemoveAll(aEntry => aEntry.Guid == aGuid);
            Entries.Insert(0, new RecentAssetEntry(aGuid, DateTime.UtcNow.Ticks));

            if (Entries.Count > MaxEntryCount)
            {
                Entries.RemoveRange(MaxEntryCount, Entries.Count - MaxEntryCount);
            }

            Save();
            OnChanged?.Invoke();
        }

        // 削除・移動されて実体が存在しないアセットを履歴から取り除く
        public static void RemoveMissing()
        {
            var removed = Entries.RemoveAll(aEntry => string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(aEntry.Guid)));
            if (removed > 0)
            {
                Save();
                OnChanged?.Invoke();
            }
        }

        public static void Clear()
        {
            Entries.Clear();
            Save();
            OnChanged?.Invoke();
        }

        public static IReadOnlyList<RecentAssetEntry> GetAll()
        {
            return Entries;
        }

        private static void Load()
        {
            var json = EditorPrefs.GetString(sPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                sEntries = new List<RecentAssetEntry>();
                return;
            }

            var wrapper = JsonUtility.FromJson<SerializableWrapper>(json);
            sEntries = wrapper?.Items ?? new List<RecentAssetEntry>();
        }

        private static void Save()
        {
            var wrapper = new SerializableWrapper { Items = Entries };
            EditorPrefs.SetString(sPrefsKey, JsonUtility.ToJson(wrapper));
        }

        [Serializable]
        private class SerializableWrapper
        {
            public List<RecentAssetEntry> Items;
        }
    }
}
