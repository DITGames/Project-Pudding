/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ActionQueue.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief FIFOのコマンドキュー
 * =====================================*/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CommandBattleCore
{
    public class ActionQueue
    {
        protected readonly LinkedList<BattleCommandBase> Items = new();
        
        public int Count => Items.Count;
        
        // キューを積む
        public virtual void Enqueue(BattleCommandBase aCommand) => Items.AddLast(aCommand);
        
        // キューを実行
        public virtual bool TryDequeue(out BattleCommandBase aOutCommand)
        {
            if (Items.Count == 0)
            {
                aOutCommand = null;
                return false;
            }
            
            aOutCommand = Items.First.Value;
            Items.RemoveFirst();
            return true;
        }
        
        // 割り込みで積む
        public virtual void EnqueueFront(BattleCommandBase aCommand) => Items.AddFirst(aCommand);
        
        public void Clear() => Items.Clear();
    }
}