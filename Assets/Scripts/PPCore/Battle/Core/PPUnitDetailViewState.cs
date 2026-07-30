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
    // 選択中ユニットのステータス詳細を表示するだけの入力ステート
    // コマンドを確定させないため、戻る以外の遷移を持たない点が他のメニューステートと異なる
    public class PPUnitDetailViewState : PPBattleMenuStateBase
    {
        // aOwner : このステートを保持する入力コントローラー
        public PPUnitDetailViewState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        // 選択中ユニットの隣に詳細ビューを出す
        // aUnit : 表示対象のユニット
        // aAnchor : ビューを配置する位置の基準
        protected override void ShowView(BattleUnit aUnit, RectTransform aAnchor)
        {
            if (aAnchor != null)
            {
                mOwner.DetailMenu.AttachTo(aAnchor);
            }
            mOwner.DetailMenu.Show(aUnit);
        }

        // 詳細ビューを閉じる
        protected override void HideView() => mOwner.DetailMenu.Hide();
        // 戻る操作を購読する
        protected override void Subscribe() => mOwner.DetailMenu.OnBackRequested += HandleBack;
        // 購読を解除する
        protected override void Unsubscribe() => mOwner.DetailMenu.OnBackRequested -= HandleBack;
        // 戻る操作。1 段ポップしてコマンド選択へ戻る
        private void HandleBack() => mOwner.Back(); // コマンド選択へ戻る
    }
}
