/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPBattleInputState.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief ユニット選択->コマンド選択->ターゲット選択をスムーズに行うためのインターフェース
 * =====================================*/

namespace PPCore
{
    public interface IPPBattleInputState
    {
        void Enter();   // 初めて入る
        void Resume();  // 一つ先から戻ってきた
        void Suspend(); // 一つ先へ進むため退避(UIを一時的に隠す)
        void Exit();    // 完全に破棄
    }
}