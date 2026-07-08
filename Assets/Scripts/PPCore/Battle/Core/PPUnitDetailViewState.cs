/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitDetailViewState.cs
 * @author hqrse
 * @date 2026/07/08
 * @brief ユニット詳細ビューチェックステート
 * =====================================*/
using UnityEngine;
using UnityEngine.UIElements;

namespace PPCore
{
    public class PPUnitDetailViewState : IPPBattleInputState
    {
        private readonly PPBattleCommandInputController mOwner;
        public PPUnitDetailViewState(PPBattleCommandInputController aOwner) => mOwner = aOwner;

        public void Enter()
        {
            var unit = mOwner.Context.Unit;
            var view = mOwner.ViewBinder.GetView(unit);
            if (view != null)
            {
                mOwner.DetailMenu.AttachTo(view.MenuAnchor);
            }
            mOwner.DetailMenu.Show(unit);
            mOwner.DetailMenu.OnBackRequested += HandleBack;
        }
        
        private void HandleBack() => mOwner.Back(); // コマンド選択へ戻る
        
        public void Suspend() => Detach();
        public void Resume() => Enter();
        public void Exit() => Detach();

        private void Detach()
        {
            mOwner.DetailMenu.OnBackRequested -= HandleBack;
            mOwner.DetailMenu.Hide();
        }
        
    }
}