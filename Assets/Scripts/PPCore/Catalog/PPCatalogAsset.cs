/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPCatalogAsset.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief アセット解決の共通基底クラス
 * =====================================*/

using UnityEngine;
using System.Collections.Generic;
using AttributeUtility;

namespace PPCore
{
    // ID から定義アセットを引くためのカタログの共通基底。初回アクセス時に ID をキーとする辞書を組んでキャッシュする
    // ID の重複はキャッシュ構築時に Debug.LogError で検出する
    public abstract class PPCatalogAsset<T> : ScriptableObject where T : ScriptableObject
    {
        /* インスペクタ設定 */

        [Label("リスト", true)] [SerializeField] private List<T> mItems = new();

        /* 状態 */

        // ID から引くための辞書。初回アクセス時に構築される
        private Dictionary<string, T> mCache;

        /* 生成 */

        // リストから ID をキーとする辞書を構築する
        // null 要素と ID を持たない要素は読み飛ばし、ID が重複している場合はエラーログを出す
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

        /* 操作 */

        // キャッシュを破棄する。実行中にリストを差し替えた場合、次回の解決で作り直される
        public void Invalidate() => mCache = null;

        // アセットから ID を取り出す。派生側で対応するプロパティを返す
        protected abstract string IdOf(T aItem);

        /* 問い合わせ */

        // ID からアセットを解決する
        public T Resolve(string aId)
        {
            mCache ??= BuildCache();
            return mCache.GetValueOrDefault(aId);
        }
    }
}
