/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitSelectState.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief ユニット選択ステート
 * =====================================*/
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine.EventSystems;

namespace PPCore
{
    public class PPUnitSelectState : IPPBattleInputState
    {
        private readonly PPBattleCommandInputController mOwner;
        public PPUnitSelectState(PPBattleCommandInputController aOwner) => mOwner = aOwner;

        private IEnumerable<BattleUnit> Allies()
            => mOwner.Manager.Context.GetParty(BattleSide.Ally).GetAliveActiveMembers();

        public void Enter()
        {
            PPBattleUnitView first = null;
            foreach (var unit in Allies())
            {
                var view = mOwner.ViewBinder.GetView(unit);
                if (view == null) continue;
                // 選択不可の条件があれば選択可能かチェックしてから設定する
                view.SetSelectable(true);
                view.OnDecided += HandleDecided;
                first ??= view;
            }
            if(first != null) EventSystem.current.SetSelectedGameObject(first.SelectableObject);
        }

        private void HandleDecided(PPBattleUnitView aView)
        {
            mOwner.Context.Unit = aView.BattleUnit;
            mOwner.Push(new PPCommandSelectState(mOwner));
        }

        public void Suspend() => DetachInteraction();
        public void Resume() => Enter();
        public void Exit() => DetachInteraction();

        private void DetachInteraction()
        {
            foreach (var unit in Allies())
            {
                var view = mOwner.ViewBinder.GetView(unit);
                if(view == null) continue;
                view.SetSelectable(false);
                view.OnDecided -= HandleDecided;
            }
        }
    }
}