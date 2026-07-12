/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitDetailViewState.cs
 * @author hqrse
 * @date 2026/07/08
 * @brief ユニット詳細ビューチェックステート
 * =====================================*/

using CommandBattleCore;
using UnityEngine;
using UnityEngine.UIElements;

namespace PPCore
{
    public class PPUnitDetailViewState : PPBattleMenuStateBase
    {
        public PPUnitDetailViewState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        protected override void ShowView(BattleUnit aUnit, RectTransform aAnchor)
        {
            if (aAnchor != null)
            {
                mOwner.DetailMenu.AttachTo(aAnchor);
            }
            mOwner.DetailMenu.Show(aUnit);
        }

        protected override void HideView() => mOwner.DetailMenu.Hide();
        protected override void Subscribe() => mOwner.DetailMenu.OnBackRequested += HandleBack;
        protected override void Unsubscribe() => mOwner.DetailMenu.OnBackRequested -= HandleBack;
        private void HandleBack() => mOwner.Back(); // コマンド選択へ戻る
    }
}