/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPCatalogAsset.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief アセット解決の共通基底クラス
 * =====================================*/

using UnityEngine;
using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    // ID から定義アセットを引くためのカタログの共通基底
    // インスペクタではリストとして持ち、初回アクセス時に ID をキーとする辞書を組んでキャッシュする
    // リストのまま毎回線形探索するのを避けつつ、インスペクタ上での編集しやすさを保つための構成
    // ID が重複していると片方が引けなくなるため、キャッシュ構築時に Debug.LogError で検出できるようにしてある
    // T : カタログが保持する定義アセットの型
    public abstract class PPCatalogAsset<T> : ScriptableObject where T : ScriptableObject
    {
        [Label("リスト", true)] [SerializeField] private List<T> mItems = new();
        // ID から引くための辞書。初回アクセス時に構築される
        private Dictionary<string, T> mCache;

        // アセットから ID を取り出す。派生側で対応するプロパティを返す
        // aItem : 対象のアセット
        // return : そのアセットの ID
        protected abstract string IdOf(T aItem);

        // ID からアセットを解決する
        // aId : 検索する ID
        // return : 該当アセット。見つからなければ null
        public T Resolve(string aId)
        {
            mCache ??= BuildCache();
            return mCache.GetValueOrDefault(aId);
        }

        // キャッシュを破棄する。実行中にリストを差し替えた場合、次回の解決で作り直される
        public void Invalidate() => mCache = null;

        // リストから ID をキーとする辞書を構築する
        // null 要素と ID を持たない要素は読み飛ばし、ID が重複している場合はエラーログを出す
        // （後勝ちで上書きされ、先に登録された方が引けなくなる）
        // return : 構築された辞書
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
