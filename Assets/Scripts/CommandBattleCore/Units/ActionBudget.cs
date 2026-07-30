/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ActionBudget.Cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 1ターン内の行動回数を管理する
 * =====================================*/

namespace CommandBattleCore
{
    /// <summary>
    /// ユニット 1 体の 1 ターン内の行動回数を管理する。
    /// <para>
    /// 通常の行動回数と、一時的に付与された追加行動を分けて持つのが要点。
    /// 消費は追加行動から先に減るため、ターン終了時のリセットで
    /// 使い残した追加行動が持ち越されることはない。
    /// </para>
    /// </summary>
    public class ActionBudget
    {
        /// <summary>基本行動回数。ターン開始時にこの値まで回復する。</summary>
        public int Max { get; set; } = 1;
        /// <summary>このターンの残り行動回数。</summary>
        public int Remaining { get; protected set; } = 1;
        /// <summary>一時的に付与された追加行動。ターンをまたいで持ち越されない。</summary>
        public int ExtraActions { get; protected set; } = 0;
        /// <summary>まだ行動できるか。通常分と追加分の合計で判定する。</summary>
        public bool CanAction => Remaining + ExtraActions > 0;

        /// <summary>
        /// ターン開始時のリセット。残り回数を上限へ戻し、未使用の追加行動を破棄する。
        /// </summary>
        public void ResetForTurn()
        {
            Remaining = Max;
            ExtraActions = 0;
        }

        /// <summary>
        /// 行動回数を 1 消費する。追加行動が残っていればそちらから先に減らす。
        /// どちらも 0 の場合は何もしない（マイナスにはならない）。
        /// </summary>
        public void Consume()
        {
            if (ExtraActions > 0) ExtraActions--;
            else if (Remaining > 0) Remaining--;
        }

        /// <summary>
        /// 追加行動を付与する。
        /// </summary>
        /// <param name="aCount">付与する回数。</param>
        public void GrantExtra(int aCount = 1) => ExtraActions += aCount;
    }
}
