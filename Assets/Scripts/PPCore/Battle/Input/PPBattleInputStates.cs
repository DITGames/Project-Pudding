/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleInputStates.cs
 * @author hqrse
 * @date 2026/07/09
 * @brief コマンド入力の各ステート（基底・コマンド選択・ユニット選択・スキル選択・対象選択・ターゲット指定・詳細表示）
 * =====================================*/

using System.Collections.Generic;

namespace PPCore
{
    // メニューを表示する入力ステートの共通実装。派生側は表示・非表示・購読・購読解除の 4 つを実装する
    // 退避と破棄は同じ処理を共有し、Resume は選択途中の内容をリセットしてから開き直す
    public abstract class PPBattleMenuStateBase : IPPBattleInputState
    {
        /* 状態 */

        // このステートを保持する入力コントローラー
        protected readonly PPBattleCommandInputController mOwner;

        /* 生成 */

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

        // 選択中ユニットについてメニューを表示し、入力を購読する
        // ユニットをまだ選んでいない段階（コマンドの選択）では、ユニットは null で渡る
        public void Enter()
        {
            ShowView(mOwner.Context.Unit);
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

        protected abstract void ShowView(BattleUnit aUnit);

        protected abstract void HideView();
    }

    // コイン型メニューに項目を並べて選ばせる入力ステートの共通実装
    // 派生側は「何を並べるか」と「決まったら何をするか」を実装する
    public abstract class PPCoinMenuStateBase : PPBattleMenuStateBase
    {
        /* 定数 */

        protected const string ClearLabel = "解除";

        /* 生成 */

        protected PPCoinMenuStateBase(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        /* 初期化・接続 */

        protected override void Subscribe()
        {
            mOwner.CoinMenu.OnDecided += HandleDecided;
            mOwner.CoinMenu.OnFocusChanged += HandleFocusChanged;
        }

        protected override void Unsubscribe()
        {
            mOwner.CoinMenu.OnDecided -= HandleDecided;
            mOwner.CoinMenu.OnFocusChanged -= HandleFocusChanged;
        }

        /* 表示 */

        // 項目を並べて開き、最初にカーソルを置く項目の強調を反映する
        protected override void ShowView(BattleUnit aUnit)
        {
            var entries = BuildEntries();
            int focus = InitialFocus();
            mOwner.CoinMenu.Show(entries, focus);
            HandleFocusChanged(mOwner.CoinMenu.FocusIndex);
        }

        protected override void HideView() => mOwner.CoinMenu.Hide();

        // 並べる項目を左から順に返す
        protected abstract IReadOnlyList<PPCoinMenuEntry> BuildEntries();

        // 開いたときにカーソルを置く項目の番号
        protected virtual int InitialFocus() => 0;

        /* イベントハンドラ */

        // 項目が決定されたときの処理
        protected abstract void HandleDecided(int aIndex);

        // カーソルが移ったときの処理
        protected virtual void HandleFocusChanged(int aIndex)
        {
        }
    }

    // ユニットのアイコンをコインに入れて並べ、どのユニットかを選ばせる入力ステートの共通実装
    // カーソルを置いたユニットだけをステージ上で元の色にし、ほかを暗くする
    public abstract class PPBattleUnitPickerStateBase : PPCoinMenuStateBase
    {
        /* 状態 */

        // 今並べている候補。コインの番号と同じ並び
        private readonly List<BattleUnit> mCandidates = new();

        /* 生成 */

        protected PPBattleUnitPickerStateBase(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        /* 表示 */

        // 候補のユニットを並べ、派生側が足す項目(解除など)をその右に続ける
        protected override IReadOnlyList<PPCoinMenuEntry> BuildEntries()
        {
            mCandidates.Clear();
            mCandidates.AddRange(Candidates());
            var entries = new List<PPCoinMenuEntry>();
            foreach (var unit in mCandidates)
            {
                entries.Add(new PPCoinMenuEntry(mOwner.ViewBinder.GetIcon(unit), unit.DisplayName));
            }
            AppendExtraEntries(entries);
            return entries;
        }

        // 前に選んでいたユニットがいれば、そこにカーソルを置く
        protected override int InitialFocus()
        {
            int index = mCandidates.IndexOf(PreferredUnit());
            return index >= 0 ? index : 0;
        }

        // 閉じるときに暗くした表示を元に戻す
        protected override void HideView()
        {
            base.HideView();
            mOwner.ViewBinder.ClearFocus();
        }

        // 選択候補となるユニットを列挙する
        protected abstract IEnumerable<BattleUnit> Candidates();

        // 開いたときにカーソルを置きたいユニット。無ければ null
        protected virtual BattleUnit PreferredUnit() => null;

        // ユニットの後ろに並べる項目を足す
        protected virtual void AppendExtraEntries(List<PPCoinMenuEntry> aEntries)
        {
        }

        /* イベントハンドラ */

        // ユニットのコインならそのユニットを、それ以外なら全員を元の色で見せる
        protected override void HandleFocusChanged(int aIndex)
        {
            if (aIndex >= 0 && aIndex < mCandidates.Count) mOwner.ViewBinder.SetFocus(mCandidates[aIndex]);
            else mOwner.ViewBinder.ClearFocus();
        }

        // ユニットのコインと、後ろに足した項目とで処理を分ける
        protected override void HandleDecided(int aIndex)
        {
            if (aIndex < mCandidates.Count) HandleUnitDecided(mCandidates[aIndex]);
            else HandleExtraDecided(aIndex - mCandidates.Count);
        }

        // ユニットが決定されたときの処理
        protected abstract void HandleUnitDecided(BattleUnit aUnit);

        // 後ろに足した項目が決定されたときの処理。番号は足した項目の中での順番
        protected virtual void HandleExtraDecided(int aExtraIndex)
        {
        }
    }

    // 「どのユニットについて操作するか」を選ぶ入力ステート。コマンドの選択のあとに挟まる
    // 味方の生存アクティブメンバーを候補にし、決定するとユニットを記録して、生成時に渡された続きを実行する
    public class PPUnitSelectState : PPBattleUnitPickerStateBase
    {
        // ユニットが決まったあとに行う処理。次のステートを積むか、その場で入力を閉じる
        private readonly System.Action mOnDecided;

        public PPUnitSelectState(PPBattleCommandInputController aOwner, System.Action aOnDecided) : base(aOwner)
        {
            mOnDecided = aOnDecided;
        }

        protected override IEnumerable<BattleUnit> Candidates()
            => mOwner.Manager.Context.GetParty(BattleSide.Ally).GetAliveActiveMembers();

        // 先の画面から戻ったときは、さっき選んだユニットにカーソルを置く
        protected override BattleUnit PreferredUnit() => mOwner.Context.Unit;

        // 選択されたユニットを記録し、続きの処理を行う
        protected override void HandleUnitDecided(BattleUnit aUnit)
        {
            mOwner.Context.Unit = aUnit;
            mOnDecided?.Invoke();
        }
    }

    // 「何をするか」（スキル・ターゲット・詳細・戻る）を選ばせる入力ステート。コマンド入力の起点になる
    // 戻る以外は、続けて対象のユニットを選ばせる
    public class PPCommandSelectState : PPCoinMenuStateBase
    {
        /* 定数 */

        private const int SkillIndex = 0;
        private const int TargetIndex = 1;
        private const int DetailIndex = 2;

        /* 生成 */

        public PPCommandSelectState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        /* 表示 */

        protected override IReadOnlyList<PPCoinMenuEntry> BuildEntries()
        {
            var menu = mOwner.CoinMenu;
            return new[]
            {
                new PPCoinMenuEntry(menu.SkillIcon, "スキル"),
                new PPCoinMenuEntry(menu.TargetIcon, "ターゲット"),
                new PPCoinMenuEntry(menu.DetailIcon, "詳細"),
                new PPCoinMenuEntry(menu.BackIcon, "戻る"),
            };
        }

        /* イベントハンドラ */

        // スキル・ターゲット・詳細はユニットを選ばせてから続きへ進み、戻るは入力を閉じる
        protected override void HandleDecided(int aIndex)
        {
            switch (aIndex)
            {
                case SkillIndex:
                    mOwner.Push(new PPUnitSelectState(mOwner, () => mOwner.Push(new PPSkillSelectState(mOwner))));
                    break;
                case TargetIndex:
                    mOwner.Push(new PPUnitSelectState(mOwner, () => mOwner.Push(new PPDesignateTargetState(mOwner))));
                    break;
                case DetailIndex:
                    mOwner.Push(new PPUnitSelectState(mOwner, () => mOwner.Push(new PPUnitDetailViewState(mOwner))));
                    break;
                default:
                    mOwner.Back();
                    break;
            }
        }
    }

    // 使用するスキルを選ばせる入力ステート。一番右の解除でスキルの選択を取り消せる
    // 味方単体が対象のスキルだけ対象選択へ進み、それ以外はその場で確定する
    public class PPSkillSelectState : PPCoinMenuStateBase
    {
        /* 状態 */

        // 今並べているスキル。コインの番号と同じ並び。解除のコインはこの後ろに続く
        private readonly List<BattleSkill> mSkills = new();

        /* 生成 */

        public PPSkillSelectState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        /* 表示 */

        // 所持スキルをコストと発動可否つきで並べ、一番右に解除を置く
        protected override IReadOnlyList<PPCoinMenuEntry> BuildEntries()
        {
            var unit = mOwner.Context.Unit;
            var catalog = mOwner.SkillVisualCatalog;
            mSkills.Clear();
            var entries = new List<PPCoinMenuEntry>();
            foreach (var skill in unit.Skills)
            {
                // 表示した時点の値だけを使うため、ソースはすぐ破棄する
                using var source = new PPBattleSkillStatusSource(skill, unit, mOwner.Manager.Context);
                // スキルごとのアイコンが無いときは、スキルの記号で代用する
                var icon = catalog != null ? catalog.Resolve(skill.SkillId)?.SkillIcon : null;
                if (icon == null) icon = mOwner.CoinMenu.SkillIcon;
                entries.Add(new PPCoinMenuEntry(icon, source.DisplayName, source.SkillGaugeCost.ToString("0.##"), source.IsCastable));
                mSkills.Add(skill);
            }
            entries.Add(new PPCoinMenuEntry(mOwner.CoinMenu.ClearIcon, ClearLabel));
            return entries;
        }

        // 選択中のスキルがあれば、そこにカーソルを置く
        protected override int InitialFocus()
        {
            int index = mSkills.IndexOf((mOwner.Context.Unit as PPBattleUnit)?.SelectedSkill);
            return index >= 0 ? index : 0;
        }

        /* イベントハンドラ */

        // スキルならコマンドを組み立て、解除ならスキルの選択を取り消して入力を閉じる
        protected override void HandleDecided(int aIndex)
        {
            if (aIndex >= mSkills.Count)
            {
                (mOwner.Context.Unit as PPBattleUnit)?.ClearSkillSelection();
                mOwner.Abort();
                return;
            }
            HandleSkillSelected(mSkills[aIndex]);
        }

        // ターゲット範囲を記録し、コマンドビルダーを仕込む
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
    }

    // スキルの対象にする味方を選ぶ入力ステート。スキル選択の最終段階で、
    // 決定すると PPBattleCommandInputController.Confirm によりコマンドが確定する
    public class PPTargetSelectState : PPBattleUnitPickerStateBase
    {
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
        protected override void HandleUnitDecided(BattleUnit aUnit)
        {
            mOwner.Context.Target = aUnit;
            mOwner.Confirm();
        }
    }

    // 選んだユニットが「狙う相手」を選ぶ入力ステート。ターゲット指定の最終段階で、一番右の解除で指定を取り消せる
    // 決定すると指定を書き換えて入力を閉じる。指定は通常攻撃と敵単体が対象のスキルに使われる
    public class PPDesignateTargetState : PPBattleUnitPickerStateBase
    {
        public PPDesignateTargetState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        // 相手陣営の生存アクティブメンバーを候補として返す。ユニットが決まっていない場合は候補を出さない
        protected override IEnumerable<BattleUnit> Candidates()
            => mOwner.Context.Unit == null
                ? System.Array.Empty<BattleUnit>()
                : mOwner.Manager.Context.GetOpponentParty(mOwner.Context.Unit.Side).GetAliveActiveMembers();

        // 今狙っている相手にカーソルを置く
        protected override BattleUnit PreferredUnit() => (mOwner.Context.Unit as PPBattleUnit)?.DesignatedTarget;

        protected override void AppendExtraEntries(List<PPCoinMenuEntry> aEntries)
            => aEntries.Add(new PPCoinMenuEntry(mOwner.CoinMenu.ClearIcon, ClearLabel));

        // 選択された相手を狙う相手として指定し、入力を閉じる
        protected override void HandleUnitDecided(BattleUnit aUnit)
        {
            (mOwner.Context.Unit as PPBattleUnit)?.DesignateTarget(aUnit);
            mOwner.Abort();
        }

        // 解除が選ばれたら、狙う相手の指定を取り消して入力を閉じる
        protected override void HandleExtraDecided(int aExtraIndex)
        {
            (mOwner.Context.Unit as PPBattleUnit)?.ClearDesignatedTarget();
            mOwner.Abort();
        }
    }

    // 選択中ユニットのステータス詳細を表示するだけの入力ステート
    // コマンドを確定させないため、戻る以外の遷移を持たない点が他のメニューステートと異なる
    public class PPUnitDetailViewState : PPBattleMenuStateBase
    {
        /* 生成 */

        public PPUnitDetailViewState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        /* 初期化・接続 */

        protected override void Subscribe() => mOwner.DetailMenu.OnBackRequested += HandleBack;

        protected override void Unsubscribe() => mOwner.DetailMenu.OnBackRequested -= HandleBack;

        /* 表示 */

        // 詳細ビューを画面いっぱいに出す
        protected override void ShowView(BattleUnit aUnit) => mOwner.DetailMenu.Show(aUnit);

        protected override void HideView() => mOwner.DetailMenu.Hide();

        /* イベントハンドラ */

        // 戻る操作。1 段ポップしてユニット選択へ戻る
        private void HandleBack() => mOwner.Back();
    }
}
