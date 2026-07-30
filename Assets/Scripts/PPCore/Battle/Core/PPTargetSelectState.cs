/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTargetSelectState.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief ターゲット選択ステート
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
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
}
