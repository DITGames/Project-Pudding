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
}
