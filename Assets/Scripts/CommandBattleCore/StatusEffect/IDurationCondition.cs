/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IDurationCondition.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 状態異常の持続条件
 * =====================================*/

namespace CommandBattleCore
{
    /// <summary>
    /// ステータスエフェクトが「いつ切れるか」を表す持続条件。
    /// <para>
    /// ターン数で切れるのか、永続なのか、何らかの条件式で切れるのかをエフェクト本体から切り離してある。
    /// <see cref="BattleUnit.TickStatusEffects"/> が毎更新で <see cref="Tick"/> を呼び、
    /// false が返った時点でエフェクトを除去する。
    /// </para>
    /// </summary>
    public interface IDurationCondition
    {
        /// <summary>
        /// 更新のたびに呼ばれる。
        /// ターン制バトルならターン毎、ATB なら一定時間毎など。
        /// </summary>
        /// <returns>効果を継続する場合 true。false を返すとエフェクトが除去される。</returns>
        bool Tick();
    }

    /// <summary>
    /// 継続時間を延長し直せる持続条件であることを示すインターフェース。
    /// <see cref="StatusEffectStackPolicy.Refresh"/> 系の重ね掛けで使われる。
    /// </summary>
    public interface IRefreshableDuration
    {
        /// <summary>継続時間を初期値へ戻す。</summary>
        void Refresh();
    }

    /// <summary>
    /// 指定ターン数が経過すると切れる持続条件。
    /// </summary>
    /// <remarks>
    /// <see cref="Tick"/> は減算してから判定するため、残り 1 の状態で呼ばれるとその場で終了する。
    /// 付与直後にも Tick が回る前提の呼び出し順になっている点に注意
    /// （<see cref="BattleSkill.NotifyUsed"/> がクールダウンを +1 しているのと同じ理由）。
    /// </remarks>
    public sealed class TurnDurationCondition : IDurationCondition, IRefreshableDuration
    {
        /// <summary>リフレッシュ時に戻す初期ターン数。</summary>
        private readonly int mInitialDuration;
        /// <summary>残りターン数。</summary>
        public int RemainingDuration {get; private set;}

        /// <param name="aDuration">持続ターン数。</param>
        public TurnDurationCondition(int aDuration)
        {
            mInitialDuration = aDuration;
            RemainingDuration = aDuration;
        }

        /// <summary>残りターン数を 1 減らし、まだ残っていれば継続を返す。</summary>
        public bool Tick() => --RemainingDuration > 0;
        /// <summary>残りターン数を初期値へ戻す。</summary>
        public void Refresh() => RemainingDuration = mInitialDuration;  // 初期ターン数に復帰する
    }

    /// <summary>
    /// 自然には切れない持続条件。解除するにはアイテムや解除スキルで明示的に除去する必要がある。
    /// </summary>
    public sealed class PermanentDurationCondition : IDurationCondition
    {
        /// <summary>常に継続を返す。</summary>
        public bool Tick() => true;
    }

    /// <summary>
    /// 任意の条件式で継続可否を制御する汎用実装。
    /// 「HP が一定以下の間だけ」といった、ターン数で表せない持続条件に使う。
    /// </summary>
    public sealed class PredicateDurationCondition : IDurationCondition
    {
        /// <summary>継続すべきかを判定する関数。</summary>
        private readonly System.Func<bool> mShouldContinueFunc;

        /// <param name="aShouldContinueFunc">継続する間 true を返す関数。</param>
        public PredicateDurationCondition(System.Func<bool> aShouldContinueFunc) => mShouldContinueFunc = aShouldContinueFunc;

        /// <summary>条件式を評価してそのまま返す。</summary>
        public bool Tick() => mShouldContinueFunc();
    }
}
