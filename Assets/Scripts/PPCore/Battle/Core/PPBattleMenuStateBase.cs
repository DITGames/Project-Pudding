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
    /// <summary>
    /// メニュー UI を表示する入力ステートの共通実装。
    /// <para>
    /// コマンド選択・スキル選択のように「選択中ユニットの隣にメニューを出す」系のステートが継承する。
    /// ライフサイクル（表示 → 購読 → 退避 → 破棄）の流れを固定し、
    /// 派生側は表示・非表示・購読・購読解除の 4 つだけを実装すればよいようにしてある。
    /// </para>
    /// <para>
    /// 退避と破棄はどちらも「UI を閉じて購読を切る」だけなので同じ処理を共有する。
    /// 戻ってきた際は <see cref="Resume"/> が選択途中の内容をリセットしてから開き直す。
    /// </para>
    /// </summary>
    public abstract class PPBattleMenuStateBase : IPPBattleInputState
    {
        /// <summary>このステートを保持する入力コントローラー。</summary>
        protected readonly PPBattleCommandInputController mOwner;

        /// <param name="aOwner">このステートを保持する入力コントローラー。</param>
        protected PPBattleMenuStateBase(PPBattleCommandInputController aOwner) => mOwner = aOwner;

        /// <summary>メニュー UI を表示する。</summary>
        /// <param name="aUnit">選択中のユニット。</param>
        /// <param name="aAnchor">メニューを配置する位置の基準。ビューが無い場合は null。</param>
        protected abstract void ShowView(BattleUnit aUnit, RectTransform aAnchor);
        /// <summary>メニュー UI を隠す。</summary>
        protected abstract void HideView();
        /// <summary>メニューの決定イベントなどを購読する。</summary>
        protected abstract void Subscribe();
        /// <summary>購読を解除する。</summary>
        protected abstract void Unsubscribe();

        /// <summary>
        /// 選択中ユニットのビューからアンカーを引いてメニューを表示し、入力を購読する。
        /// </summary>
        public void Enter()
        {
            var unit = mOwner.Context.Unit;
            var view = mOwner.ViewBinder.GetView(unit);
            ShowView(unit, view?.MenuAnchor);
            Subscribe();
        }

        /// <summary>
        /// 先のステートから戻ってきたときの復帰処理。
        /// ユニットの選択は保ったまま、その先で選んだ内容（スキル・対象）だけを破棄して開き直す。
        /// </summary>
        public void Resume()
        {
            mOwner.Context.ClearSelectionKeepingUnit();
            Enter();
        }

        /// <summary>先へ進むため退避する。UI を閉じて購読を切る。</summary>
        public void Suspend() => Detach();
        /// <summary>破棄する。UI を閉じて購読を切る。</summary>
        public void Exit() => Detach();

        /// <summary>購読解除と UI の非表示をまとめて行う。</summary>
        private void Detach()
        {
            Unsubscribe();
            HideView();
        }
    }
}
