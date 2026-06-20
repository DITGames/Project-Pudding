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
    public class BattleStateMachine
    {
        public BattleState Current { get; private set; } = BattleState.None;

        // ステート変更デリゲート（遷移前、遷移後）
        public event Action<BattleState, BattleState> OnStateChanged;
        
        // 遷移可否検証のチェック関数　採用先が遷移ルールを差し込める
        public Func<BattleState, BattleState, bool> TransitionValidator { get; set; }

        // 遷移
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