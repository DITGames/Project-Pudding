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
    /// <summary>
    /// ID から定義アセットを引くためのカタログの共通基底。
    /// <para>
    /// インスペクタではリストとして持ち、初回アクセス時に ID をキーとする辞書を組んでキャッシュする。
    /// リストのまま毎回線形探索するのを避けつつ、インスペクタ上での編集しやすさを保つための構成。
    /// </para>
    /// <para>
    /// ID が重複していると片方が引けなくなるため、キャッシュ構築時に
    /// <see cref="Debug.LogError"/> で検出できるようにしてある。
    /// </para>
    /// </summary>
    /// <typeparam name="T">カタログが保持する定義アセットの型。</typeparam>
    public abstract class PPCatalogAsset<T> : ScriptableObject where T : ScriptableObject
    {
        /// <summary>登録されている定義アセット。</summary>
        [Label("リスト", true)] [SerializeField] private List<T> mItems = new();
        /// <summary>ID から引くための辞書。初回アクセス時に構築される。</summary>
        private Dictionary<string, T> mCache;

        /// <summary>
        /// アセットから ID を取り出す。派生側で対応するプロパティを返す。
        /// </summary>
        /// <param name="aItem">対象のアセット。</param>
        /// <returns>そのアセットの ID。</returns>
        protected abstract string IdOf(T aItem);

        /// <summary>
        /// ID からアセットを解決する。
        /// </summary>
        /// <param name="aId">検索する ID。</param>
        /// <returns>該当アセット。見つからなければ null。</returns>
        public T Resolve(string aId)
        {
            mCache ??= BuildCache();
            return mCache.GetValueOrDefault(aId);
        }

        /// <summary>
        /// キャッシュを破棄する。実行中にリストを差し替えた場合、次回の解決で作り直される。
        /// </summary>
        public void Invalidate() => mCache = null;

        /// <summary>
        /// リストから ID をキーとする辞書を構築する。
        /// null 要素と ID を持たない要素は読み飛ばし、ID が重複している場合はエラーログを出す
        /// （後勝ちで上書きされ、先に登録された方が引けなくなる）。
        /// </summary>
        /// <returns>構築された辞書。</returns>
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
