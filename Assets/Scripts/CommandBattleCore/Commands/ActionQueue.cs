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
    // 実行待ちコマンドを保持する FIFO キュー
    // 通常のコマンドは末尾へ積み、リアクション（反撃など）は EnqueueFront で先頭へ割り込ませる
    // 両端操作が要るため内部は LinkedList
    // 優先度付きキューなどへ差し替えられるよう各メソッドは virtual にしてある
    public class ActionQueue
    {
        // 実行待ちコマンドの実体。先頭が次に実行される
        protected readonly LinkedList<BattleCommandBase> Items = new();

        // キューに残っているコマンド数
        public int Count => Items.Count;

        // コマンドをキュー末尾へ積む
        // aCommand : 積むコマンド
        public virtual void Enqueue(BattleCommandBase aCommand) => Items.AddLast(aCommand);

        // キュー先頭のコマンドを取り出す
        // aOutCommand : 取り出されたコマンド。キューが空なら null
        // return : 取り出せた場合 true。キューが空なら false
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

        // コマンドをキュー先頭へ割り込ませる。リアクションを次の 1 手として優先実行させるために使う
        // aCommand : 割り込ませるコマンド
        public virtual void EnqueueFront(BattleCommandBase aCommand) => Items.AddFirst(aCommand);

        // キューを空にする。バトル終了時に未実行コマンドを破棄する用途
        public void Clear() => Items.Clear();
    }
}
