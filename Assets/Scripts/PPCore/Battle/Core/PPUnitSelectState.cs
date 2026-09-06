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
        public void Enter()
        {
            mOwner.UnitSelectMenu.Show(Candidates());
            mOwner.UnitSelectMenu.OnUnitSelected += HandleDecided;
        }

        // コマンド入力の起点（スタック最下段）のため、戻ってきたときは選択内容を最初から選び直す
        public void Resume()
        {
            mOwner.Context.Clear();
            Enter();
        }

        // 先へ進むため退避する。購読を解除してメニューを隠す
        public void Suspend() => Detach();
        // 破棄する。購読を解除してメニューを隠す
        public void Exit() => Detach();

        // 購読解除とメニューの非表示をまとめて行う
        private void Detach()
        {
            mOwner.UnitSelectMenu.OnUnitSelected -= HandleDecided;
            mOwner.UnitSelectMenu.Hide();
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
