/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ActionBudget.Cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 1ターン内の行動回数を管理する
 * =====================================*/
using UnityEngine;

namespace CommandBattleCore
{
    public class ActionBudget
    {
        // 基本行動回数
        public int Max { get; set; } = 1;
        // このターンの残り行動回数
        public int Remaining { get; protected set; } = 1;
        // 一時的に付与された追加行動
        public int ExtraActions { get; protected set; } = 0;
        // まだ行動可能?
        public bool CanAction => Remaining + ExtraActions > 0;

        // ターン開始時のリセット
        public void ResetForTurn()
        {
            Remaining = Max;
            ExtraActions = 0;
        }
        
        // 行動回数の消費
        public void Consume()
        {
            if (ExtraActions > 0) ExtraActions--;
            else if (Remaining > 0) Remaining--;
        }
        
        // 追加行動の獲得
        public void GrantExtra(int aCount = 1) => ExtraActions += aCount;
    }
}