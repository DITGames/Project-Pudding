/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPItemCommand.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief アイテムコマンドの拡張版
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    public class PPItemCommand : ItemCommand
    {
        private readonly PPItemDefinition mDefinition;
        
        public PPItemCommand(BattleUnit aUnit, PPItemDefinition aDefinition, ITargetResolver aResolver)
            : base(aUnit, aDefinition, aResolver) => mDefinition = aDefinition;

        public override void Execute(BattleContext aContext)
        {
            if (aContext.GetParty(Source.Side) is not PPBattleParty party) return;
            if (!party.ResourcePool.CanConsumeResource(mDefinition.Cost)) return;
            if (!party.Inventory.TryConsume(mDefinition))
            {
                Debug.Log("アイテムが足りません");
                return;
            }
            base.Execute(aContext);
        }
    }
}