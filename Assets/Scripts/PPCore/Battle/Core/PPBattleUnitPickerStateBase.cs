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
    // 盤上のユニットを直接選ばせる入力ステートの共通実装
    // 行動するユニットの選択と、スキルの対象選択が継承する
    // メニューを出すのではなく、候補ユニットのビュー自体を選択可能にして決定イベントを購読する方式
    // 派生側は「候補は誰か」と「決まったら何をするか」の 2 つだけを実装する
    // 選択可能化・購読・先頭要素へのフォーカス設定・後始末はここが引き受ける
    public abstract class PPBattleUnitPickerStateBase : IPPBattleInputState
    {
        // このステートを保持する入力コントローラー
        protected readonly PPBattleCommandInputController mOwner;

        // aOwner : このステートを保持する入力コントローラー
        protected PPBattleUnitPickerStateBase(PPBattleCommandInputController aOwner) => mOwner = aOwner;

        // 選択候補となるユニットを列挙する
        protected abstract IEnumerable<BattleUnit> Candidates();
        // 候補が決定されたときの処理
        // aView : 決定されたユニットのビュー
        protected abstract void HandleDecided(PPBattleUnitView aView);

        // 候補ユニットのビューを選択可能にして決定イベントを購読し、
        // 先頭の候補にフォーカスを当てる（コントローラー操作の起点になる）
        // ビューを持たない候補は読み飛ばす
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

        // 先へ進むため退避する。選択可能状態と購読を解除する
        public virtual void Suspend() => Detach();
        // 戻ってきたときの復帰処理。候補を選び直せる状態にする
        public virtual void Resume() => Enter();
        // 破棄する。選択可能状態と購読を解除する
        public virtual void Exit() => Detach();

        // 候補ユニットの選択可能状態を解除し、決定イベントの購読を切る
        // 購読が残るとステートを抜けた後も入力を拾ってしまうため、退避・破棄の双方で必ず通す
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
