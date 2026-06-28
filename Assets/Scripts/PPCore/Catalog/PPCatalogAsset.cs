/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPCatalogAsset.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief PPアセット解決の共通基底クラス
 * =====================================*/
using UnityEngine;
using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    public abstract class PPCatalogAsset<T> : ScriptableObject where T : ScriptableObject
    {
        [Label("リスト", true)] [SerializeField] private List<T> mItems = new();
        private Dictionary<string, T> mCache;

        protected abstract string IdOf(T aItem);

        public T Resolve(string aId)
        {
            mCache ??= BuildCache();
            return mCache.GetValueOrDefault(aId);
        }

        public void Invalidate() => mCache = null;

        private Dictionary<string, T> BuildCache()
        {
            var dict = new Dictionary<string, T>();
            foreach (var item in mItems)
            {
                if (item == null) continue;
                var key = IdOf(item);
                if (key == null) continue;
                if (dict.ContainsKey(key))
                {
                    Debug.LogError($"[{name}] 重複 ID : {key} 上書きされます");
                }

                dict[key] = item;
            }

            return dict;
        }
    }
}