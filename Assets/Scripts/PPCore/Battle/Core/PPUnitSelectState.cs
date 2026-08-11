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
}
