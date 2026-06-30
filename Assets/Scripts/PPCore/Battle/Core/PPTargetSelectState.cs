/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTargetSelectState.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief PPターゲット選択コマンド
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PPCore
{
    public class PPTargetSelectState : IPPBattleInputState
    {
        private readonly PPBattleCommandInputController mOwner;
        public PPTargetSelectState(PPBattleCommandInputController aOwner) => mOwner = aOwner;

        private IEnumerable<BattleUnit> Candidates()
        {
            var ctx = mOwner.Manager.Context;
            var actor = mOwner.Context.Unit;
            return mOwner.Context.Skill.DefaultTargetResolver is SingleAllyResolver
                ? ctx.GetParty(actor.Side).GetAliveActiveMembers()
                : ctx.GetOpponentParty(actor.Side).GetAliveActiveMembers();
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