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
    /// <summary>
    /// 選択中ユニットのステータス詳細を表示するだけの入力ステート。
    /// コマンドを確定させないため、戻る以外の遷移を持たない点が他のメニューステートと異なる。
    /// </summary>
    public class PPUnitDetailViewState : PPBattleMenuStateBase
    {
        /// <param name="aOwner">このステートを保持する入力コントローラー。</param>
        public PPUnitDetailViewState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        /// <summary>選択中ユニットの隣に詳細ビューを出す。</summary>
        /// <param name="aUnit">表示対象のユニット。</param>
        /// <param name="aAnchor">ビューを配置する位置の基準。</param>
        protected override void ShowView(BattleUnit aUnit, RectTransform aAnchor)
        {
            if (aAnchor != null)
            {
                mOwner.DetailMenu.AttachTo(aAnchor);
            }
            mOwner.DetailMenu.Show(aUnit);
        }

        /// <summary>詳細ビューを閉じる。</summary>
        protected override void HideView() => mOwner.DetailMenu.Hide();
        /// <summary>戻る操作を購読する。</summary>
        protected override void Subscribe() => mOwner.DetailMenu.OnBackRequested += HandleBack;
        /// <summary>購読を解除する。</summary>
        protected override void Unsubscribe() => mOwner.DetailMenu.OnBackRequested -= HandleBack;
        /// <summary>戻る操作。1 段ポップしてコマンド選択へ戻る。</summary>
        private void HandleBack() => mOwner.Back(); // コマンド選択へ戻る
    }
}
