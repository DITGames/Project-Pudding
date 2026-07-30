/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ActionBudget.Cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 1ターン内の行動回数を管理する
 * =====================================*/

namespace CommandBattleCore
{
    // ユニット 1 体の 1 ターン内の行動回数を管理する
    // 通常の行動回数と、一時的に付与された追加行動を分けて持つのが要点
    // 消費は追加行動から先に減るため、ターン終了時のリセットで
    // 使い残した追加行動が持ち越されることはない
    public class ActionBudget
    {
        // 基本行動回数。ターン開始時にこの値まで回復する
        public int Max { get; set; } = 1;
        // このターンの残り行動回数
        public int Remaining { get; protected set; } = 1;
        // 一時的に付与された追加行動。ターンをまたいで持ち越されない
        public int ExtraActions { get; protected set; } = 0;
        // まだ行動できるか。通常分と追加分の合計で判定する
        public bool CanAction => Remaining + ExtraActions > 0;

        // ターン開始時のリセット。残り回数を上限へ戻し、未使用の追加行動を破棄する
        public void ResetForTurn()
        {
            Remaining = Max;
            ExtraActions = 0;
        }

        // 行動回数を 1 消費する。追加行動が残っていればそちらから先に減らす
        // どちらも 0 の場合は何もしない（マイナスにはならない）
        public void Consume()
        {
            if (ExtraActions > 0) ExtraActions--;
            else if (Remaining > 0) Remaining--;
        }

        // 追加行動を付与する
        // aCount : 付与する回数
        public void GrantExtra(int aCount = 1) => ExtraActions += aCount;
    }
}
