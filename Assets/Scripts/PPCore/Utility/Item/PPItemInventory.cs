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
    public class PPItemInventory
    {
        private readonly Dictionary<PPItemDefinition, int> mItems = new();
        public event Action Changed;

        public PPItemInventory(IReadOnlyDictionary<PPItemDefinition, int> aList)
        {
            BuildInventory(aList);
        }

        public void BuildInventory(IReadOnlyDictionary<PPItemDefinition, int> aList)
        {
            
            foreach (var i in aList)
            {
                Add(i.Key, i.Value);
            }
        }

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

        public IEnumerable<PPItemDefinition> UsableItems()
        {
            foreach (var i in mItems)
            {
                if(i.Value > 0)
                    yield return i.Key;
            }
        }
        
        public int CountOf(PPItemDefinition aItem) => mItems.GetValueOrDefault(aItem);

        public void Add(PPItemDefinition aItem, int aCount)
        {
            mItems[aItem] = CountOf(aItem) + aCount;
            Changed?.Invoke();
        }

        public bool TryConsume(PPItemDefinition aItem)
        {
            if(CountOf(aItem) <= 0) return false;
            mItems[aItem]--;
            Changed?.Invoke();
            return true;
        }
    }
}