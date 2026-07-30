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
    // メニュー UI を表示する入力ステートの共通実装
    // コマンド選択・スキル選択のように「選択中ユニットの隣にメニューを出す」系のステートが継承する
    // ライフサイクル（表示 → 購読 → 退避 → 破棄）の流れを固定し、
    // 派生側は表示・非表示・購読・購読解除の 4 つだけを実装すればよいようにしてある
    // 退避と破棄はどちらも「UI を閉じて購読を切る」だけなので同じ処理を共有する
    // 戻ってきた際は Resume が選択途中の内容をリセットしてから開き直す
    public abstract class PPBattleMenuStateBase : IPPBattleInputState
    {
        // このステートを保持する入力コントローラー
        protected readonly PPBattleCommandInputController mOwner;

        // aOwner : このステートを保持する入力コントローラー
        protected PPBattleMenuStateBase(PPBattleCommandInputController aOwner) => mOwner = aOwner;

        // メニュー UI を表示する
        // aUnit : 選択中のユニット
        // aAnchor : メニューを配置する位置の基準。ビューが無い場合は null
        protected abstract void ShowView(BattleUnit aUnit, RectTransform aAnchor);
        // メニュー UI を隠す
        protected abstract void HideView();
        // メニューの決定イベントなどを購読する
        protected abstract void Subscribe();
        // 購読を解除する
        protected abstract void Unsubscribe();

        // 選択中ユニットのビューからアンカーを引いてメニューを表示し、入力を購読する
        public void Enter()
        {
            var unit = mOwner.Context.Unit;
            var view = mOwner.ViewBinder.GetView(unit);
            ShowView(unit, view?.MenuAnchor);
            Subscribe();
        }

        // 先のステートから戻ってきたときの復帰処理
        // ユニットの選択は保ったまま、その先で選んだ内容（スキル・対象）だけを破棄して開き直す
        public void Resume()
        {
            mOwner.Context.ClearSelectionKeepingUnit();
            Enter();
        }

        // 先へ進むため退避する。UI を閉じて購読を切る
        public void Suspend() => Detach();
        // 破棄する。UI を閉じて購読を切る
        public void Exit() => Detach();

        // 購読解除と UI の非表示をまとめて行う
        private void Detach()
        {
            Unsubscribe();
            HideView();
        }
    }
}
