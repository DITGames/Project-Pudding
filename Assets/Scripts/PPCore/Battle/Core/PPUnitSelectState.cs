/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitSelectState.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief ユニット選択ステート
 * =====================================*/
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PPCore
{
    public class PPUnitSelectState : PPBattleUnitPickerStateBase
    {
        public PPUnitSelectState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        protected override IEnumerable<BattleUnit> Candidates()
        {
            return mOwner.Manager.Context.GetParty(BattleSide.Ally).GetAliveActiveMembers();
        }

        protected override void HandleDecided(PPBattleUnitView aView)
        {
            mOwner.Context.Unit = aView.BattleUnit;
            mOwner.Push(new PPCommandSelectState(mOwner));
        }
    }
}