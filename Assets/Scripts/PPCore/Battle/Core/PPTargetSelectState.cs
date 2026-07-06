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
    public class PPTargetSelectState : IPPBattleInputState
    {
        private readonly PPBattleCommandInputController mOwner;
        public PPTargetSelectState(PPBattleCommandInputController aOwner) => mOwner = aOwner;

        // 対象の取得
        private IEnumerable<BattleUnit> Candidates()
        {
            var ctx = mOwner.Manager.Context;
            var unit = mOwner.Context.Unit;
            var scope = mOwner.Context.TargetScope ?? TargetScope.SingleEnemy;
            // TargetScopeをもとに選択対象を変更する
            return PPTargeting.IsAllySide(scope)
                ? ctx.GetParty(unit.Side).GetAliveActiveMembers()
                : ctx.GetOpponentParty(unit.Side).GetAliveActiveMembers();
        }

        public void Enter()
        {
            PPBattleUnitView first = null;
            foreach (var unit in Candidates())
            {
                var view = mOwner.ViewBinder.GetView(unit);
                if (view == null) continue;
                view.SetSelectable(true);
                view.OnClicked += HandleClicked;
                first ??= view;
            }

            if (first != null)
            {
                EventSystem.current.SetSelectedGameObject(first.SelectableObject);
            }
        }

        private void HandleClicked(PPBattleUnitView aView)
        {
            mOwner.Context.Target = aView.BattleUnit;
            mOwner.Confirm();
        }

        public void Suspend() => Detach();
        public void Resume() => Enter();
        public void Exit() => Detach();

        private void Detach()
        {
            foreach (var unit in Candidates())
            {
                var view = mOwner.ViewBinder.GetView(unit);
                if(view == null) continue;
                view.SetSelectable(false);
                view.OnClicked -= HandleClicked;
            }
        }
    }
}