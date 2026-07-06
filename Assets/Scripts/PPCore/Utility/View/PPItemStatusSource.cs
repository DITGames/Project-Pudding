/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPItemStatusSource.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief アイテム状態ソースベース
 * =====================================*/

using System;
using UnityEngine;

namespace PPCore
{
    public class PPItemStatusSource : IPPItemStatusSource
    {
        private readonly PPItemDefinition mDefinition;
        private readonly PPBattleParty mParty;
        public event Action Changed;
        
        public string DisplayName => mDefinition.DisplayName;
        public int Cost => mDefinition.Cost;
        public int Count => mParty.Inventory.CountOf(mDefinition);

        public bool IsUsable =>
            mParty.ResourcePool.CanConsumeResource(mDefinition.Cost) && Count > 0;
        

        public PPItemStatusSource(PPItemDefinition aDefinition, PPBattleParty aParty)
        {
            mDefinition = aDefinition;
            mParty = aParty;
            mParty.Inventory.Changed += Raise;
        }
        
        private void Raise() => Changed?.Invoke();
        public void Dispose() => mParty.Inventory.Changed -= Raise;
    }
}