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
    // パーティが共有するアイテムの所持数を管理するインベントリ
    // アイテム定義そのものをキーにした辞書で持つ。所持数 0 のエントリも残るため、
    // 使用可能なアイテムを列挙する際は個数で絞り込む必要がある
    // 増減時に Changed を発火するので、UI 側はこれを購読して表示を更新する
    public class PPItemInventory
    {
        // アイテム定義ごとの所持数。0 のエントリも残りうる
        private readonly Dictionary<PPItemDefinition, int> mItems = new();
        // 所持数が変化したときに発火する
        public event Action Changed;

        // aList : 初期所持アイテムと個数。null なら空のインベントリになる
        public PPItemInventory(IReadOnlyDictionary<PPItemDefinition, int> aList)
        {
            BuildInventory(aList);
        }

        // 指定された内容を現在のインベントリへ加算する
        // 中身を置き換えるのではなく積み増す点に注意
        // aList : 追加するアイテムと個数。null なら何もしない
        public void BuildInventory(IReadOnlyDictionary<PPItemDefinition, int> aList)
        {
            if (aList == null) return;

            foreach (var i in aList)
            {
                Add(i.Key, i.Value);
            }
        }

        // 使用可能なアイテムを 1 つでも持っているか。所持数 0 のエントリは数えない
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

        // 所持数が 1 以上のアイテムを列挙する。アイテムメニューの表示項目になる
        // return : 使用可能なアイテム定義の列
        public IEnumerable<PPItemDefinition> UsableItems()
        {
            foreach (var i in mItems)
            {
                if(i.Value > 0)
                    yield return i.Key;
            }
        }

        // 指定アイテムの所持数を取得する。未所持なら 0
        // aItem : 対象のアイテム定義
        public int CountOf(PPItemDefinition aItem) => mItems.GetValueOrDefault(aItem);

        // アイテムを加算する
        // aItem : 対象のアイテム定義
        // aCount : 加算する個数
        public void Add(PPItemDefinition aItem, int aCount)
        {
            mItems[aItem] = CountOf(aItem) + aCount;
            Changed?.Invoke();
        }

        // アイテムを 1 つ消費する。所持していなければ何もせず失敗を返す
        // aItem : 対象のアイテム定義
        // return : 消費できた場合 true
        public bool TryConsume(PPItemDefinition aItem)
        {
            if(CountOf(aItem) <= 0) return false;
            mItems[aItem]--;
            Changed?.Invoke();
            return true;
        }
    }
}
