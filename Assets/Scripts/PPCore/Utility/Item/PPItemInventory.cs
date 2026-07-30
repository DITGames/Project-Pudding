/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPItemInventory.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief インベントリ
 * =====================================*/
using System;
using System.Collections.Generic;

namespace PPCore
{
    /// <summary>
    /// パーティが共有するアイテムの所持数を管理するインベントリ。
    /// <para>
    /// アイテム定義そのものをキーにした辞書で持つ。所持数 0 のエントリも残るため、
    /// 使用可能なアイテムを列挙する際は個数で絞り込む必要がある。
    /// </para>
    /// <para>
    /// 増減時に <see cref="Changed"/> を発火するので、UI 側はこれを購読して表示を更新する。
    /// </para>
    /// </summary>
    public class PPItemInventory
    {
        /// <summary>アイテム定義ごとの所持数。0 のエントリも残りうる。</summary>
        private readonly Dictionary<PPItemDefinition, int> mItems = new();
        /// <summary>所持数が変化したときに発火する。</summary>
        public event Action Changed;

        /// <param name="aList">初期所持アイテムと個数。null なら空のインベントリになる。</param>
        public PPItemInventory(IReadOnlyDictionary<PPItemDefinition, int> aList)
        {
            BuildInventory(aList);
        }

        /// <summary>
        /// 指定された内容を現在のインベントリへ加算する。
        /// 中身を置き換えるのではなく積み増す点に注意。
        /// </summary>
        /// <param name="aList">追加するアイテムと個数。null なら何もしない。</param>
        public void BuildInventory(IReadOnlyDictionary<PPItemDefinition, int> aList)
        {
            if (aList == null) return;

            foreach (var i in aList)
            {
                Add(i.Key, i.Value);
            }
        }

        /// <summary>使用可能なアイテムを 1 つでも持っているか。所持数 0 のエントリは数えない。</summary>
        public bool HasAny
        {
            get
            {
                foreach (var c in mItems.Values)
                    if (c > 0)
                        return true;
                return false;
            }
        }

        /// <summary>
        /// 所持数が 1 以上のアイテムを列挙する。アイテムメニューの表示項目になる。
        /// </summary>
        /// <returns>使用可能なアイテム定義の列。</returns>
        public IEnumerable<PPItemDefinition> UsableItems()
        {
            foreach (var i in mItems)
            {
                if(i.Value > 0)
                    yield return i.Key;
            }
        }

        /// <summary>指定アイテムの所持数を取得する。未所持なら 0。</summary>
        /// <param name="aItem">対象のアイテム定義。</param>
        public int CountOf(PPItemDefinition aItem) => mItems.GetValueOrDefault(aItem);

        /// <summary>
        /// アイテムを加算する。
        /// </summary>
        /// <param name="aItem">対象のアイテム定義。</param>
        /// <param name="aCount">加算する個数。</param>
        public void Add(PPItemDefinition aItem, int aCount)
        {
            mItems[aItem] = CountOf(aItem) + aCount;
            Changed?.Invoke();
        }

        /// <summary>
        /// アイテムを 1 つ消費する。所持していなければ何もせず失敗を返す。
        /// </summary>
        /// <param name="aItem">対象のアイテム定義。</param>
        /// <returns>消費できた場合 true。</returns>
        public bool TryConsume(PPItemDefinition aItem)
        {
            if(CountOf(aItem) <= 0) return false;
            mItems[aItem]--;
            Changed?.Invoke();
            return true;
        }
    }
}
