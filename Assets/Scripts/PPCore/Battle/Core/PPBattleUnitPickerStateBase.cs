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
    /// <summary>
    /// 盤上のユニットを直接選ばせる入力ステートの共通実装。
    /// <para>
    /// 行動するユニットの選択と、スキルの対象選択が継承する。
    /// メニューを出すのではなく、候補ユニットのビュー自体を選択可能にして
    /// 決定イベントを購読する方式。
    /// </para>
    /// <para>
    /// 派生側は「候補は誰か」と「決まったら何をするか」の 2 つだけを実装する。
    /// 選択可能化・購読・先頭要素へのフォーカス設定・後始末はここが引き受ける。
    /// </para>
    /// </summary>
    public abstract class PPBattleUnitPickerStateBase : IPPBattleInputState
    {
        /// <summary>このステートを保持する入力コントローラー。</summary>
        protected readonly PPBattleCommandInputController mOwner;

        /// <param name="aOwner">このステートを保持する入力コントローラー。</param>
        protected PPBattleUnitPickerStateBase(PPBattleCommandInputController aOwner) => mOwner = aOwner;

        /// <summary>選択候補となるユニットを列挙する。</summary>
        protected abstract IEnumerable<BattleUnit> Candidates();
        /// <summary>候補が決定されたときの処理。</summary>
        /// <param name="aView">決定されたユニットのビュー。</param>
        protected abstract void HandleDecided(PPBattleUnitView aView);

        /// <summary>
        /// 候補ユニットのビューを選択可能にして決定イベントを購読し、
        /// 先頭の候補にフォーカスを当てる（コントローラー操作の起点になる）。
        /// ビューを持たない候補は読み飛ばす。
        /// </summary>
        public virtual void Enter()
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

        /// <summary>先へ進むため退避する。選択可能状態と購読を解除する。</summary>
        public virtual void Suspend() => Detach();
        /// <summary>戻ってきたときの復帰処理。候補を選び直せる状態にする。</summary>
        public virtual void Resume() => Enter();
        /// <summary>破棄する。選択可能状態と購読を解除する。</summary>
        public virtual void Exit() => Detach();

        /// <summary>
        /// 候補ユニットの選択可能状態を解除し、決定イベントの購読を切る。
        /// 購読が残るとステートを抜けた後も入力を拾ってしまうため、退避・破棄の双方で必ず通す。
        /// </summary>
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
