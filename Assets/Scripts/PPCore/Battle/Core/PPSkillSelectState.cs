/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPSkillSelectState.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief スキル選択ステート
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 使用するスキルを選ばせる入力ステート
    // 選んだスキルのターゲット範囲に応じて、対象選択へ進むかその場で確定するかが分かれる
    public class PPSkillSelectState : PPBattleMenuStateBase
    {
        // このステートが一度でも表示されたか
        // 最初の表示（コマンド選択からの遷移）だけ上からスライドする入れ替え演出にし、
        // 対象選択から戻ってきた場合（Resume）は従来どおり即時に表示する
        private bool mHasShownOnce;
        // 対象選択へ進むための一時退避（Suspend）かどうか
        // true のときだけ HideView を即時にする。コマンド選択へ戻る（Exit）場合は常にスライド演出にする
        private bool mSuspendingForTarget;

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

            if (!mHasShownOnce)
            {
                mHasShownOnce = true;
                mOwner.SkillMenu.ShowFromAbove(aUnit, mOwner.Manager.Context);
            }
            else
            {
                mOwner.SkillMenu.Show(aUnit, mOwner.Manager.Context);
            }
        }

        // スキルメニューを閉じる
        // 対象選択への一時退避なら即時に、コマンド選択へ戻るなら上にスライドする入れ替え演出にする
        protected override void HideView()
        {
            if (mSuspendingForTarget)
            {
                mSuspendingForTarget = false;
                mOwner.SkillMenu.Hide();
            }
            else
            {
                mOwner.SkillMenu.HideSlideUp();
            }
        }

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
            // 対象選択へ進む場合は一時退避であることを示すフラグを立ててから Push する（入れ替え演出をしないため）
            if (PPTargeting.NeedsManualTarget(scope))
            {
                mSuspendingForTarget = true;
                mOwner.Push(new PPTargetSelectState(mOwner));
            }
            else
            {
                mOwner.Confirm();
            }
        }

        // 戻る操作。1 段ポップしてコマンド選択へ戻る
        private void HandleBack() => mOwner.Back(); // コマンド選択へ戻る
    }
}
