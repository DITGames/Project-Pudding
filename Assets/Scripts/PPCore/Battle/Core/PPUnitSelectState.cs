/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitSelectState.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief ユニット選択ステート
 * =====================================*/
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PPCore
{
    /// <summary>
    /// 「誰が行動するか」を選ぶ入力ステート。コマンド入力の起点になる。
    /// 味方の生存アクティブメンバーを候補にし、決定するとコマンド選択へ進む。
    /// </summary>
    public class PPUnitSelectState : PPBattleUnitPickerStateBase
    {
        /// <param name="aOwner">このステートを保持する入力コントローラー。</param>
        public PPUnitSelectState(PPBattleCommandInputController aOwner) : base(aOwner)
        {
        }

        /// <summary>味方陣営の生存アクティブメンバーを候補として返す。</summary>
        protected override IEnumerable<BattleUnit> Candidates()
        {
            return mOwner.Manager.Context.GetParty(BattleSide.Ally).GetAliveActiveMembers();
        }

        /// <summary>選択されたユニットを記録し、コマンド選択ステートへ進む。</summary>
        /// <param name="aView">決定されたユニットのビュー。</param>
        protected override void HandleDecided(PPBattleUnitView aView)
        {
            mOwner.Context.Unit = aView.BattleUnit;
            mOwner.Push(new PPCommandSelectState(mOwner));
        }
    }
}
