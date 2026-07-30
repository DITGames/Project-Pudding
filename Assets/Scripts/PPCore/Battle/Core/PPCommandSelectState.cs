/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPCommandSelectState.cs
 * @author hqrse
 * @date 2026/07/01
 * @brief コマンド選択ステート
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
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
}
