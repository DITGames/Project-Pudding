/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleUnitPickerStateBase.cs
 * @author hqrse
 * @date 2026/07/09
 * @brief バトル中のユニット選択系ビューのベースクラス
 * =====================================*/
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PPCore
{
    public abstract class PPBattleUnitPickerStateBase : IPPBattleInputState
    {
        protected readonly PPBattleCommandInputController mOwner;
        protected PPBattleUnitPickerStateBase(PPBattleCommandInputController aOwner) => mOwner = aOwner;
        
        protected abstract IEnumerable<BattleUnit> Candidates();
        protected abstract void HandleDecided(PPBattleUnitView aView);

        public void Enter()
        {
            PPBattleUnitView first = null;
            foreach (var unit in Candidates())
            {
                var view = mOwner.ViewBinder.GetView(unit);
                if(view == null) continue;
                view.SetSelectable(true);
                view.OnDecided += HandleDecided;
                first ??= view;
            }

            if (first != null)
            {
                EventSystem.current.SetSelectedGameObject(first.SelectableObject);
            }
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
                view.OnDecided -= HandleDecided;
            }
        }
    }
}