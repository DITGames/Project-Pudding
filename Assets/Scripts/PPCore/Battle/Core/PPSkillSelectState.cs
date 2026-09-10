/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillSelectState.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief スキル選択ステート
 * =====================================*/

using CommandBattleCore;

namespace PPCore
{
    // 使用するスキルを選ばせる入力ステート
    // 選んだスキルのターゲット範囲に応じて、対象選択へ進むかその場で確定するかが分かれる
    // ユニット選択メニューと同じ固定位置に表示し、Push/Back の方向に応じた
    // スライド+フェードの入退場演出を出したいため、PPBattleMenuStateBase は使わず
    // PPUnitSelectState と同様に IPPBattleInputState を直接実装する
    public class PPSkillSelectState : IPPBattleInputState
    {
        // このステートを保持する入力コントローラー
        private readonly PPBattleCommandInputController mOwner;

        // aOwner : このステートを保持する入力コントローラー
        public PPSkillSelectState(PPBattleCommandInputController aOwner) => mOwner = aOwner;

        // スキルメニューを開き、各操作を購読する
        // 先へ進む方向からの遷移なので、上から現れる演出（Down）で表示する
        public void Enter() => ShowMenu(PPBattleTransitionDirection.Down);

        // 先のステート（対象選択・詳細確認）から戻ってきたときの復帰処理
        // ユニットの選択は保ったまま、その先で選んだ内容（スキル・対象）だけを破棄して開き直す
        // 戻ってきた方向からの遷移なので、下から現れる演出（Up）で表示する
        public void Resume()
        {
            mOwner.Context.ClearSelectionKeepingUnit();
            ShowMenu(PPBattleTransitionDirection.Up);
        }

        // 先へ進むため退避する。購読を解除し、下へ抜ける演出でメニューを隠す
        public void Suspend() => Detach(PPBattleTransitionDirection.Down);
        // 破棄する。購読を解除し、上へ抜ける演出でメニューを隠す
        public void Exit() => Detach(PPBattleTransitionDirection.Up);

        // 選択中ユニットのスキルメニューを表示し、決定・戻る・詳細確認を購読する
        // コンテキストを渡すことで、メニュー側が発動可否を見て項目の有効・無効を切り替える
        // aDirection : 入場演出の向き
        private void ShowMenu(PPBattleTransitionDirection aDirection)
        {
            mOwner.SkillMenu.Show(mOwner.Context.Unit, mOwner.Manager.Context, aDirection);
            Subscribe();
        }

        // 購読解除とメニューの非表示をまとめて行う
        // aDirection : 退場演出の向き
        private void Detach(PPBattleTransitionDirection aDirection)
        {
            Unsubscribe();
            mOwner.SkillMenu.Hide(aDirection);
        }

        // スキルの決定・戻る・詳細確認操作を購読する
        private void Subscribe()
        {
            mOwner.SkillMenu.OnSkillSelected += HandleSkillSelected;
            mOwner.SkillMenu.OnBackRequested += HandleBack;
            mOwner.SkillMenu.OnDetailRequested += HandleDetail;
        }

        // 購読を解除する
        private void Unsubscribe()
        {
            mOwner.SkillMenu.OnSkillSelected -= HandleSkillSelected;
            mOwner.SkillMenu.OnBackRequested -= HandleBack;
            mOwner.SkillMenu.OnDetailRequested -= HandleDetail;
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

        // 戻る操作。1 段ポップしてユニット選択へ戻る
        private void HandleBack() => mOwner.Back();

        // 詳細確認操作。選択中ユニットの詳細ビューへ進む
        private void HandleDetail() => mOwner.Push(new PPUnitDetailViewState(mOwner));
    }
}
