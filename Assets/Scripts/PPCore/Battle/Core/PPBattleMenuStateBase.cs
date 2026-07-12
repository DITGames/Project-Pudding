/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleMenuStateBase.cs
 * @author hqrse
 * @date 2026/07/09
 * @brief バトル中のメニュー系ビューのベースクラス
 * =====================================*/
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    public abstract class PPBattleMenuStateBase : IPPBattleInputState
    {
        protected readonly PPBattleCommandInputController mOwner;
        protected PPBattleMenuStateBase(PPBattleCommandInputController aOwner) => mOwner = aOwner;
        
        protected abstract void ShowView(BattleUnit aUnit, RectTransform aAnchor);
        protected abstract void HideView();
        protected abstract void Subscribe();
        protected abstract void Unsubscribe();

        public void Enter()
        {
            var unit = mOwner.Context.Unit;
            var view = mOwner.ViewBinder.GetView(unit);
            ShowView(unit, view?.MenuAnchor);
            Subscribe();
        }

        public void Resume()
        {
            mOwner.Context.ClearSelectionKeepingUnit();
            Enter();
        }
        
        public void Suspend() => Detach();
        public void Exit() => Detach();

        private void Detach()
        {
            Unsubscribe();
            HideView();
        }
    }
}