/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPBattleInputState.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief ユニット選択->コマンド選択->ターゲット選択をスムーズに行うためのインターフェース
 * =====================================*/

namespace PPCore
{
    // コマンド入力の 1 段階を表すステート
    // PPBattleCommandInputController がこれをスタックで積み下ろしすることで、
    // ユニット選択 → コマンド選択 → スキル選択 → 対象選択という多段の入力と、
    // 任意の段階からの「戻る」を表現する
    // Suspend と Exit の違いが要点
    // 先へ進むときは Suspend で退避するだけなので、戻ってきたときに
    // Resume で状態を復元できる。Exit は破棄で、もう戻ってこない
    public interface IPPBattleInputState
    {
        // 初めてこのステートに入るときに呼ばれる。UI の表示と購読を行う
        void Enter();
        // 一つ先のステートから戻ってきたときに呼ばれる
        void Resume();
        // 一つ先へ進むため退避するときに呼ばれる。UI を一時的に隠す
        void Suspend();
        // 完全に破棄されるときに呼ばれる。購読解除と後始末を行う
        void Exit();
    }
}
