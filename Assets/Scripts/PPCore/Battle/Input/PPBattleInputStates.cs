/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleInputStates.cs
 * @author hqrse
 * @date 2026/07/09
 * @brief コマンド入力の各ステート（基底・コマンド選択・ユニット選択・スキル選択・対象選択・ターゲット指定・詳細表示）
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PPCore
{
    // メニュー UI を表示する入力ステートの共通実装。派生側は表示・非表示・購読・購読解除の 4 つを実装する
    // 退避と破棄は同じ処理を共有し、Resume は選択途中の内容をリセットしてから開き直す
    public abstract class PPBattleMenuStateBase : IPPBattleInputState
    {
        /* 状態 */

        // このステートを保持する入力コントローラー
        protected readonly PPBattleCommandInputController mOwner;

        /* 生成 */

        // aOwner : このステートを保持する入力コントローラー
        protected PPBattleMenuStateBase(PPBattleCommandInputController aOwner) => mOwner = aOwner;

        /* 初期化・接続 */

        // メニューの決定イベントなどを購読する
        protected abstract void Subscribe();

        // 購読を解除する
        protected abstract void Unsubscribe();

        // 購読解除と UI の非表示をまとめて行う
        private void Detach()
        {
            Unsubscribe();
            HideView();
        }

        /* 操作 */

        // 選択中ユニットのビューからアンカーを引いてメニューを表示し、入力を購読する
        // ユニットをまだ選んでいない段階（コマンドの選択）では、ユニットもアンカーも null で渡る
        public void Enter()
        {
            var unit = mOwner.Context.Unit;
            var view = unit != null ? mOwner.ViewBinder.GetView(unit) : null;
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

        /* 表示 */

        // メニュー UI を表示する
        protected abstract void ShowView(BattleUnit aUnit, RectTransform aAnchor);

        // メニュー UI を隠す
        protected abstract void HideView();
    }

    // 盤上のユニットを直接選ばせる入力ステートの共通実装
    // 派生側は「候補は誰か」と「決まったら何をするか」の 2 つだけを実装する
    public abstract class PPBattleUnitPickerStateBase : IPPBattleInputState
    {
        /* 状態 */

        // このステートを保持する入力コントローラー
        protected readonly PPBattleCommandInputController mOwner;

        /* 生成 */

        // aOwner : このステートを保持する入力コントローラー
        protected PPBattleUnitPickerStateBase(PPBattleCommandInputController aOwner) => mOwner = aOwner;

        /* 初期化・接続 */

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

        /* 操作 */

        // 候補ユニットのビューを選択可能にして決定イベントを購読し、先頭の候補にフォーカスを当てる
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

        // 選択候補となるユニットを列挙する
        protected abstract IEnumerable<BattleUnit> Candidates();

        /* イベントハンドラ */

        // 候補が決定されたときの処理
        // aView : 決定されたユニットのビュー
        protected abstract void HandleDecided(PPBattleUnitView aView);
    }

    // 「どのユニットについて操作するか」を選ぶ入力ステート。コマンドの選択のあとに挟まる
    // 味方の生存アクティブメンバーを候補にし、決定するとユニットを記録して、生成時に渡された続きを実行する
    public class PPUnitSelectState : PPBattleUnitPickerStateBase
    {
        // ユニットが決まったあとに行う処理。次のステートを積むか、その場で入力を閉じる
        private readonly System.Action mOnDecided;

        // aOwner : このステートを保持する入力コントローラー
        // aOnDecided : ユニットが決まったあとに行う処理
        public PPUnitSelectState(PPBattleCommandInputController aOwner, System.Action aOnDecided) : base(aOwner)
        {
            mOnDecided = aOnDecided;
        }

        // 味方陣営の生存アクティブメンバーを候補として返す
        protected override IEnumerable<BattleUnit> Candidates()
            => mOwner.Manager.Context.GetParty(BattleSide.Ally).GetAliveActiveMembers();

        // 選択されたユニットを記録し、続きの処理を行う
        // aView : 決定されたユニットのビュー
        protected override void HandleDecided(PPBattleUnitView aView)
        {
            mOwner.Context.Unit = aView.BattleUnit;
            mOnDecided?.Invoke();
        }
    }

    // 「何をするか」（ターゲットの指定・スキルの選択・解除・詳細確認）を選ばせる入力ステート。コマンド入力の起点になる
    // どれを選んでも、続けて対象のユニットを選ばせる
    public class PPCommandSelectState : PPBattleMenuStateBase
    {
        /* 生成 */

        // aOwner : このステートを保持する入力コントローラー
        public PPCommandSelectState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        /* 初期化・接続 */

        // 各コマンドの決定と戻る操作を購読する
        protected override void Subscribe()
        {
            mOwner.CommandMenu.OnTarget += HandleTarget;
            mOwner.CommandMenu.OnSkill += HandleSkill;
            mOwner.CommandMenu.OnClear += HandleClear;
            mOwner.CommandMenu.OnDetail += HandleDetail;
            mOwner.CommandMenu.OnBackRequested += mOwner.Back;
        }

        // 購読を解除する
        protected override void Unsubscribe()
        {
            mOwner.CommandMenu.OnTarget -= HandleTarget;
            mOwner.CommandMenu.OnSkill -= HandleSkill;
            mOwner.CommandMenu.OnClear -= HandleClear;
            mOwner.CommandMenu.OnDetail -= HandleDetail;
            mOwner.CommandMenu.OnBackRequested -= mOwner.Back;
        }

        /* 表示 */

        // コマンドメニューを出す。ユニットを選ぶ前なので、ユニットの隣ではなく元の位置に出す
        protected override void ShowView(BattleUnit aUnit, RectTransform aAnchor) => mOwner.CommandMenu.Show();

        // コマンドメニューを閉じる
        protected override void HideView() => mOwner.CommandMenu.Hide();

        /* イベントハンドラ */

        // ターゲットを選んだときの処理。ユニットを選ばせ、続けて狙う敵を選ばせる
        private void HandleTarget()
            => mOwner.Push(new PPUnitSelectState(mOwner, () => mOwner.Push(new PPDesignateTargetState(mOwner))));

        // スキルを選んだときの処理。ユニットを選ばせ、続けてスキルを選ばせる
        private void HandleSkill()
            => mOwner.Push(new PPUnitSelectState(mOwner, () => mOwner.Push(new PPSkillSelectState(mOwner))));

        // 解除を選んだときの処理。選んだユニットのスキルの選択と狙う相手の指定を取り消して入力を閉じる
        private void HandleClear()
            => mOwner.Push(new PPUnitSelectState(mOwner, () =>
            {
                if (mOwner.Context.Unit is PPBattleUnit unit)
                {
                    unit.ClearSkillSelection();
                    unit.ClearDesignatedTarget();
                }
                mOwner.Abort();
            }));

        // 詳細を選んだときの処理。ユニットを選ばせ、続けて詳細を表示する
        private void HandleDetail()
            => mOwner.Push(new PPUnitSelectState(mOwner, () => mOwner.Push(new PPUnitDetailViewState(mOwner))));
    }

    // 使用するスキルを選ばせる入力ステート
    // 味方単体が対象のスキルだけ対象選択へ進み、それ以外はその場で確定する
    public class PPSkillSelectState : PPBattleMenuStateBase
    {
        /* 生成 */

        // aOwner : このステートを保持する入力コントローラー
        public PPSkillSelectState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        /* 初期化・接続 */

        // スキルの決定と戻る操作を購読する
        protected override void Subscribe()
        {
            mOwner.SkillMenu.OnSkillSelected += HandleSkillSelected;
            mOwner.SkillMenu.OnBackRequested += HandleBack;
        }

        // 購読を解除する
        protected override void Unsubscribe()
        {
            mOwner.SkillMenu.OnSkillSelected -= HandleSkillSelected;
            mOwner.SkillMenu.OnBackRequested -= HandleBack;
        }

        /* 表示 */

        // 選択中ユニットの隣にスキルメニューを出す
        // コンテキストを渡すことで、メニュー側が発動可否を見て項目の有効・無効を切り替える
        protected override void ShowView(BattleUnit aUnit, RectTransform aAnchor)
        {
            if (aAnchor != null)
            {
                mOwner.SkillMenu.AttachTo(aAnchor);
            }
            mOwner.SkillMenu.Show(aUnit, mOwner.Manager.Context);
        }

        // スキルメニューを閉じる
        protected override void HideView() => mOwner.SkillMenu.Hide();

        /* イベントハンドラ */

        // スキルが選ばれたときの処理。ターゲット範囲を記録し、コマンドビルダーを仕込む
        // 敵単体が対象のスキルは「狙う相手」の指定を使うため、ここでは対象を選ばせない
        private void HandleSkillSelected(BattleSkill aSkill)
        {
            var unit = mOwner.Context.Unit;
            var scope = (aSkill.SourceDefinition as SkillDefinition)?.TargetScope ?? TargetScope.SingleEnemy;
            mOwner.Context.Skill = aSkill;
            mOwner.Context.TargetScope = scope;
            mOwner.Context.CommandBuilder = tgt =>
                new PPSkillCommand(unit, aSkill, mOwner.BuildResolver(aSkill.DefaultTargetResolver, tgt));

            // 味方単体が対象のスキルだけ、選択の時点で対象を選ばせる
            if (scope == TargetScope.SingleAlly)
                mOwner.Push(new PPTargetSelectState(mOwner));
            else
                mOwner.Confirm();
        }

        // 戻る操作。1 段ポップして前の選択へ戻る
        private void HandleBack() => mOwner.Back();
    }

    // スキルの対象にする味方を選ぶ入力ステート。スキル選択の最終段階で、
    // 決定すると PPBattleCommandInputController.Confirm によりコマンドが確定する
    public class PPTargetSelectState : PPBattleUnitPickerStateBase
    {
        // aOwner : このステートを保持する入力コントローラー
        public PPTargetSelectState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        // 選択中スキルのターゲット範囲から、味方側か敵側かを判定して候補を返す
        // 範囲が未設定の場合は味方単体として扱う
        protected override IEnumerable<BattleUnit> Candidates()
        {
            var ctx = mOwner.Manager.Context;
            var unit = mOwner.Context.Unit;
            var scope = mOwner.Context.TargetScope ?? TargetScope.SingleAlly;
            return PPTargeting.IsAllySide(scope)
                ? ctx.GetParty(unit.Side).GetAliveActiveMembers()
                : ctx.GetOpponentParty(unit.Side).GetAliveActiveMembers();
        }

        // 選択された対象を記録し、コマンドを確定させる
        // aView : 決定されたユニットのビュー
        protected override void HandleDecided(PPBattleUnitView aView)
        {
            mOwner.Context.Target = aView.BattleUnit;
            mOwner.Confirm();
        }
    }

    // 選んだユニットが「狙う相手」を選ぶ入力ステート。ターゲット指定の最終段階
    // 決定すると指定を書き換えて入力を閉じる。指定は通常攻撃と敵単体が対象のスキルに使われる
    public class PPDesignateTargetState : PPBattleUnitPickerStateBase
    {
        // aOwner : このステートを保持する入力コントローラー
        public PPDesignateTargetState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        // 相手陣営の生存アクティブメンバーを候補として返す
        protected override IEnumerable<BattleUnit> Candidates()
            => mOwner.Manager.Context.GetOpponentParty(mOwner.Context.Unit.Side).GetAliveActiveMembers();

        // 選択された相手を狙う相手として指定し、入力を閉じる
        // aView : 決定されたユニットのビュー
        protected override void HandleDecided(PPBattleUnitView aView)
        {
            (mOwner.Context.Unit as PPBattleUnit)?.DesignateTarget(aView.BattleUnit);
            mOwner.Abort();
        }
    }
    // 選択中ユニットのステータス詳細を表示するだけの入力ステート
    // コマンドを確定させないため、戻る以外の遷移を持たない点が他のメニューステートと異なる
    public class PPUnitDetailViewState : PPBattleMenuStateBase
    {
        /* 生成 */

        // aOwner : このステートを保持する入力コントローラー
        public PPUnitDetailViewState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        /* 初期化・接続 */

        // 戻る操作を購読する
        protected override void Subscribe() => mOwner.DetailMenu.OnBackRequested += HandleBack;

        // 購読を解除する
        protected override void Unsubscribe() => mOwner.DetailMenu.OnBackRequested -= HandleBack;

        /* 表示 */

        // 選択中ユニットの隣に詳細ビューを出す
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

        /* イベントハンドラ */

        // 戻る操作。1 段ポップしてユニット選択へ戻る
        private void HandleBack() => mOwner.Back();
    }
}
