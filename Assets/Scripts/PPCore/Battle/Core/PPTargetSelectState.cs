/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTargetSelectState.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief ターゲット選択ステート
 * =====================================*/
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine.EventSystems;

namespace PPCore
{
    public class PPTargetSelectState : PPBattleUnitPickerStateBase
    {
        public PPTargetSelectState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }
        protected override IEnumerable<BattleUnit> Candidates()
        {
            var ctx = mOwner.Manager.Context;
            var unit = mOwner.Context.Unit;
            var scope = mOwner.Context.TargetScope ?? TargetScope.SingleAlly;
            return PPTargeting.IsAllySide(scope)
                ? ctx.GetParty(unit.Side).GetAliveActiveMembers()
                : ctx.GetOpponentParty(unit.Side).GetAliveActiveMembers();
        }
        protected override void HandleDecided(PPBattleUnitView aView)
        {
            mOwner.Context.Target = aView.BattleUnit;
            mOwner.Confirm();
        }
    }
}