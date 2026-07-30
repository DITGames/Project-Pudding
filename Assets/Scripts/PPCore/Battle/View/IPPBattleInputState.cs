/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPBattleInputState.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief ユニット選択->コマンド選択->ターゲット選択をスムーズに行うためのインターフェース
 * =====================================*/

namespace PPCore
{
    /// <summary>
    /// コマンド入力の 1 段階を表すステート。
    /// <para>
    /// <see cref="PPBattleCommandInputController"/> がこれをスタックで積み下ろしすることで、
    /// ユニット選択 → コマンド選択 → スキル選択 → 対象選択という多段の入力と、
    /// 任意の段階からの「戻る」を表現する。
    /// </para>
    /// <para>
    /// <see cref="Suspend"/> と <see cref="Exit"/> の違いが要点。
    /// 先へ進むときは Suspend で退避するだけなので、戻ってきたときに
    /// <see cref="Resume"/> で状態を復元できる。Exit は破棄で、もう戻ってこない。
    /// </para>
    /// </summary>
    public interface IPPBattleInputState
    {
        /// <summary>初めてこのステートに入るときに呼ばれる。UI の表示と購読を行う。</summary>
        void Enter();
        /// <summary>一つ先のステートから戻ってきたときに呼ばれる。</summary>
        void Resume();
        /// <summary>一つ先へ進むため退避するときに呼ばれる。UI を一時的に隠す。</summary>
        void Suspend();
        /// <summary>完全に破棄されるときに呼ばれる。購読解除と後始末を行う。</summary>
        void Exit();
    }
}
