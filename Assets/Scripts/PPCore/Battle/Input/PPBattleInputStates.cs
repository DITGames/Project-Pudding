/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleInputStates.cs
 * @author hqrse
 * @date 2026/07/09
 * @brief コマンド入力の各ステート（基底・ユニット選択・コマンド選択・スキル選択・対象選択・詳細表示）
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

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

    // 「誰が行動するか」を選ぶ入力ステート。コマンド入力の起点になる
    // 味方の生存アクティブメンバーを候補にし、決定するとコマンド選択へ進む
    public class PPUnitSelectState : PPBattleUnitPickerStateBase
    {
        // aOwner : このステートを保持する入力コントローラー
        public PPUnitSelectState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        // 味方陣営の生存アクティブメンバーのうち、まだ行動回数が残っているものを候補として返す
        // 行動回数を使い切ったユニットはこのティックでは動かせないため、選択させない
        // 基底が購読時と解除時の 2 回この結果を走査するため、遅延評価にせずリストとして確定させる
        protected override IEnumerable<BattleUnit> Candidates()
        {
            var candidates = new List<BattleUnit>();
            foreach (var unit in mOwner.Manager.Context.GetParty(BattleSide.Ally).GetAliveActiveMembers())
            {
                if (!unit.Actions.CanAction) continue;

                candidates.Add(unit);
            }
            return candidates;
        }

        // 選択されたユニットを記録し、コマンド選択ステートへ進む
        // aView : 決定されたユニットのビュー
        protected override void HandleDecided(PPBattleUnitView aView)
        {
            mOwner.Context.Unit = aView.BattleUnit;
            mOwner.Push(new PPCommandSelectState(mOwner));
        }
    }

    // 選択したユニットに「何をさせるか」（攻撃・スキル・詳細確認）を選ばせる入力ステート
    // 攻撃はその場で対象選択へ、スキルと詳細はそれぞれ専用のステートへ進む
    public class PPCommandSelectState : PPBattleMenuStateBase
    {
        // aOwner : このステートを保持する入力コントローラー
        public PPCommandSelectState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        // 選択中ユニットの隣にコマンドメニューを出す
        // スキルを 1 つも持たないユニットではスキル項目を出さない
        // aUnit : 選択中のユニット
        // aAnchor : メニューを配置する位置の基準
        protected override void ShowView(BattleUnit aUnit, RectTransform aAnchor)
        {
            if (aAnchor != null)
            {
                mOwner.CommandMenu.AttachTo(aAnchor);
            }
            mOwner.CommandMenu.Show(aUnit.Skills.Count > 0);
        }

        // コマンドメニューを閉じる
        protected override void HideView() => mOwner.CommandMenu.Hide();

        // 各コマンドの決定と戻る操作を購読する
        protected override void Subscribe()
        {
            mOwner.CommandMenu.OnAttack += HandleAttack;
            mOwner.CommandMenu.OnSkill += HandleSkill;
            mOwner.CommandMenu.OnDetail += HandleDetail;
            mOwner.CommandMenu.OnBackRequested += mOwner.Back;
        }

        // 購読を解除する
        protected override void Unsubscribe()
        {
            mOwner.CommandMenu.OnAttack -= HandleAttack;
            mOwner.CommandMenu.OnSkill -= HandleSkill;
            mOwner.CommandMenu.OnDetail -= HandleDetail;
            mOwner.CommandMenu.OnBackRequested -= mOwner.Back;
        }

        // 通常攻撃を選んだときの処理
        // 対象範囲を敵単体に固定し、対象が決まった時点でコマンドを組み立てられるよう
        // ビルダーを仕込んでから対象選択へ進む
        private void HandleAttack()
        {
            var unit = (PPBattleUnit)mOwner.Context.Unit;
            mOwner.Context.TargetScope = TargetScope.SingleEnemy;
            mOwner.Context.CommandBuilder = tgt =>
                new PPAttackCommand(unit, mOwner.BuildResolver(new SingleEnemyResolver(), tgt));
            mOwner.Push(new PPTargetSelectState(mOwner));
        }

        // スキルを選んだときの処理。スキル選択ステートへ進む
        private void HandleSkill() => mOwner.Push(new PPSkillSelectState(mOwner));

        // 詳細を選んだときの処理。ユニット詳細ステートへ進む
        private void HandleDetail() => mOwner.Push(new PPUnitDetailViewState(mOwner));
    }

    // 使用するスキルを選ばせる入力ステート
    // 選んだスキルのターゲット範囲に応じて、対象選択へ進むかその場で確定するかが分かれる
    public class PPSkillSelectState : PPBattleMenuStateBase
    {
        // aOwner : このステートを保持する入力コントローラー
        public PPSkillSelectState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        // 選択中ユニットの隣にスキルメニューを出す
        // コンテキストを渡すことで、メニュー側が発動可否を見て項目の有効・無効を切り替える
        // aUnit : 選択中のユニット
        // aAnchor : メニューを配置する位置の基準
        protected override void ShowView(BattleUnit aUnit, RectTransform aAnchor)
        {
            if (aAnchor != null)
            {
                mOwner.SkillMenu.AttachTo(aAnchor);
            }
            mOwner.SkillMenu.Show(aUnit, mOwner.Manager.Context, mOwner.ActionLedger);
        }

        // スキルメニューを閉じる
        protected override void HideView() => mOwner.SkillMenu.Hide();

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

        // スキルが選ばれたときの処理
        // 定義からターゲット範囲を引いて選択内容へ記録し、コマンドビルダーを仕込む
        // 単体対象なら対象選択へ進み、全体・自己対象ならそのまま確定させる
        // aSkill : 選択されたスキル
        private void HandleSkillSelected(BattleSkill aSkill)
        {
            var unit = mOwner.Context.Unit;
            var scope = (aSkill.SourceDefinition as SkillDefinition)?.TargetScope ?? TargetScope.SingleEnemy;
            mOwner.Context.Skill = aSkill;
            mOwner.Context.TargetScope = scope;
            mOwner.Context.CommandBuilder = tgt =>
                new PPSkillCommand(unit, aSkill, mOwner.BuildResolver(aSkill.DefaultTargetResolver, tgt));

            // スキルの効果対象によってターゲット選択と行動決定を分岐
            if(PPTargeting.NeedsManualTarget(scope))
                mOwner.Push(new PPTargetSelectState(mOwner));
            else
                mOwner.Confirm();
        }

        // 戻る操作。1 段ポップしてコマンド選択へ戻る
        private void HandleBack() => mOwner.Back(); // コマンド選択へ戻る
    }

    // 「誰を狙うか」を選ぶ入力ステート。コマンド入力の最終段階で、
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
