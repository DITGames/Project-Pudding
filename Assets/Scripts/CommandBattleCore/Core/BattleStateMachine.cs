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
    // BattleState の現在値を保持し、遷移と通知だけを担う軽量ステートマシン
    // 遷移の可否ルール自体は持たず、TransitionValidator として採用先から差し込む設計
    // 未設定なら任意のステートへ遷移できる
    public class BattleStateMachine
    {
        // 現在のバトルステート
        public BattleState Current { get; protected set; } = BattleState.None;

        // ステート変更デリゲート（遷移前、遷移後）
        public event Action<BattleState, BattleState> OnStateChanged;

        // 遷移可否検証のチェック関数。採用先が遷移ルールを差し込める
        // 引数は（遷移前, 遷移後）で、false を返すと遷移は行われない
        public Func<BattleState, BattleState, bool> TransitionValidator { get; set; }

        // 指定ステートへ遷移する。バリデータが拒否した場合は現在値を変えずに終わる
        // aState : 遷移先のステート
        // return : 遷移した場合 true。バリデータに拒否された場合 false
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
