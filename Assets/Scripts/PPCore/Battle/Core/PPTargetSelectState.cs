/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTargetSelectState.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief ターゲット選択ステート
 * =====================================*/
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine.EventSystems;

namespace PPCore
{
    /// <summary>
    /// 「誰を狙うか」を選ぶ入力ステート。コマンド入力の最終段階で、
    /// 決定すると <see cref="PPBattleCommandInputController.Confirm"/> によりコマンドが確定する。
    /// </summary>
    public class PPTargetSelectState : PPBattleUnitPickerStateBase
    {
        /// <param name="aOwner">このステートを保持する入力コントローラー。</param>
        public PPTargetSelectState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        /// <summary>
        /// 選択中スキルのターゲット範囲から、味方側か敵側かを判定して候補を返す。
        /// 範囲が未設定の場合は味方単体として扱う。
        /// </summary>
        protected override IEnumerable<BattleUnit> Candidates()
        {
            var ctx = mOwner.Manager.Context;
            var unit = mOwner.Context.Unit;
            var scope = mOwner.Context.TargetScope ?? TargetScope.SingleAlly;
            return PPTargeting.IsAllySide(scope)
                ? ctx.GetParty(unit.Side).GetAliveActiveMembers()
                : ctx.GetOpponentParty(unit.Side).GetAliveActiveMembers();
        }

        /// <summary>選択された対象を記録し、コマンドを確定させる。</summary>
        /// <param name="aView">決定されたユニットのビュー。</param>
        protected override void HandleDecided(PPBattleUnitView aView)
        {
            mOwner.Context.Target = aView.BattleUnit;
            mOwner.Confirm();
        }
    }
}
