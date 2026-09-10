/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitSelectState.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief ユニット選択ステート
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    // 「誰が行動するか」を選ぶ入力ステート。コマンド入力の起点になる
    // 味方の生存アクティブメンバーを候補にし、決定するとスキル選択へ進む
    // 通常攻撃・コマンド選択は廃止し、行動はスキルからのみ選ばせる
    // 盤面表示（PPBattleUnitView）とは切り離した専用のユニット選択メニュー（PPBattleUnitSelectMenuView）を使う。
    // 対象選択（PPTargetSelectState）は引き続き盤面のユニットを直接クリックする方式のままなので、
    // PPBattleUnitPickerStateBase は継承しない
    public class PPUnitSelectState : IPPBattleInputState
    {
        // このステートを保持する入力コントローラー
        private readonly PPBattleCommandInputController mOwner;

        // aOwner : このステートを保持する入力コントローラー
        public PPUnitSelectState(PPBattleCommandInputController aOwner) => mOwner = aOwner;

        // ユニット選択メニューを開き、決定イベントを購読する
        // 先へ進む方向からの遷移なので、上から現れる演出（Down）で表示する
        public void Enter() => ShowMenu(PPBattleTransitionDirection.Down);

        // コマンド入力の起点（スタック最下段）のため、戻ってきたときは選択内容を最初から選び直す
        // 戻ってきた方向からの遷移なので、下から現れる演出（Up）で表示する
        public void Resume()
        {
            mOwner.Context.Clear();
            ShowMenu(PPBattleTransitionDirection.Up);
        }

        // 先へ進むため退避する。購読を解除し、下へ抜ける演出でメニューを隠す
        public void Suspend() => Detach(PPBattleTransitionDirection.Down);
        // 破棄する。購読を解除し、上へ抜ける演出でメニューを隠す
        public void Exit() => Detach(PPBattleTransitionDirection.Up);

        // メニューを表示し、決定イベントを購読する
        // aDirection : 入場演出の向き
        private void ShowMenu(PPBattleTransitionDirection aDirection)
        {
            mOwner.UnitSelectMenu.Show(Candidates(), aDirection);
            mOwner.UnitSelectMenu.OnUnitSelected += HandleDecided;
        }

        // 購読解除とメニューの非表示をまとめて行う
        // aDirection : 退場演出の向き
        private void Detach(PPBattleTransitionDirection aDirection)
        {
            mOwner.UnitSelectMenu.OnUnitSelected -= HandleDecided;
            mOwner.UnitSelectMenu.Hide(aDirection);
        }

        // 味方陣営の生存アクティブメンバーのうち、まだ行動回数が残っているものを候補として返す
        // 行動回数を使い切ったユニットはこのティックでは動かせないため、選択させない
        private IEnumerable<BattleUnit> Candidates()
        {
            var candidates = new List<BattleUnit>();
            foreach (var unit in mOwner.Manager.Context.GetParty(BattleSide.Ally).GetAliveActiveMembers())
            {
                if (!unit.Actions.CanAction) continue;

                candidates.Add(unit);
            }
            return candidates;
        }

        // 選択されたユニットを記録し、スキル選択ステートへ進む
        // aUnit : 決定されたユニット
        private void HandleDecided(BattleUnit aUnit)
        {
            mOwner.Context.Unit = aUnit;
            mOwner.Push(new PPSkillSelectState(mOwner));
        }
    }
}
