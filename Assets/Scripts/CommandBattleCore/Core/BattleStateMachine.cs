/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleStateMachine.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトル進行ステートの管理
 * =====================================*/

using System;

namespace CommandBattleCore
{
    /// <summary>
    /// <see cref="BattleState"/> の現在値を保持し、遷移と通知だけを担う軽量ステートマシン。
    /// <para>
    /// 遷移の可否ルール自体は持たず、<see cref="TransitionValidator"/> として採用先から差し込む設計。
    /// 未設定なら任意のステートへ遷移できる。
    /// </para>
    /// </summary>
    public class BattleStateMachine
    {
        /// <summary>現在のバトルステート。</summary>
        public BattleState Current { get; protected set; } = BattleState.None;

        /// <summary>ステート変更デリゲート（遷移前、遷移後）</summary>
        public event Action<BattleState, BattleState> OnStateChanged;

        /// <summary>
        /// 遷移可否検証のチェック関数。採用先が遷移ルールを差し込める。
        /// 引数は（遷移前, 遷移後）で、false を返すと遷移は行われない。
        /// </summary>
        public Func<BattleState, BattleState, bool> TransitionValidator { get; set; }

        /// <summary>
        /// 指定ステートへ遷移する。バリデータが拒否した場合は現在値を変えずに終わる。
        /// </summary>
        /// <param name="aState">遷移先のステート。</param>
        /// <returns>遷移した場合 true。バリデータに拒否された場合 false。</returns>
        public bool TransitionTo(BattleState aState)
        {
            if (TransitionValidator != null && !TransitionValidator(Current, aState))
            {
                return false;
            }

            var prev = Current;
            Current = aState;
            OnStateChanged?.Invoke(prev, Current);
            return true;
        }
    }
}
