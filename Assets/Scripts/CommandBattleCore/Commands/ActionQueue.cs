/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ActionQueue.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief FIFOのコマンドキュー
 * =====================================*/

using System.Collections.Generic;

namespace CommandBattleCore
{
    /// <summary>
    /// 実行待ちコマンドを保持する FIFO キュー。
    /// <para>
    /// 通常のコマンドは末尾へ積み、リアクション（反撃など）は <see cref="EnqueueFront"/> で先頭へ割り込ませる。
    /// 両端操作が要るため内部は <see cref="LinkedList{T}"/>。
    /// 優先度付きキューなどへ差し替えられるよう各メソッドは virtual にしてある。
    /// </para>
    /// </summary>
    public class ActionQueue
    {
        /// <summary>実行待ちコマンドの実体。先頭が次に実行される。</summary>
        protected readonly LinkedList<BattleCommandBase> Items = new();

        /// <summary>キューに残っているコマンド数。</summary>
        public int Count => Items.Count;

        /// <summary>
        /// コマンドをキュー末尾へ積む。
        /// </summary>
        /// <param name="aCommand">積むコマンド。</param>
        public virtual void Enqueue(BattleCommandBase aCommand) => Items.AddLast(aCommand);

        /// <summary>
        /// キュー先頭のコマンドを取り出す。
        /// </summary>
        /// <param name="aOutCommand">取り出されたコマンド。キューが空なら null。</param>
        /// <returns>取り出せた場合 true。キューが空なら false。</returns>
        public virtual bool TryDequeue(out BattleCommandBase aOutCommand)
        {
            if (Items.Count == 0)
            {
                aOutCommand = null;
                return false;
            }

            aOutCommand = Items.First.Value;
            Items.RemoveFirst();
            return true;
        }

        /// <summary>
        /// コマンドをキュー先頭へ割り込ませる。リアクションを次の 1 手として優先実行させるために使う。
        /// </summary>
        /// <param name="aCommand">割り込ませるコマンド。</param>
        public virtual void EnqueueFront(BattleCommandBase aCommand) => Items.AddFirst(aCommand);

        /// <summary>キューを空にする。バトル終了時に未実行コマンドを破棄する用途。</summary>
        public void Clear() => Items.Clear();
    }
}
