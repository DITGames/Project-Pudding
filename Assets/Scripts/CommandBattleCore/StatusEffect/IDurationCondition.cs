/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IDurationCondition.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 状態異常の持続条件
 * =====================================*/

namespace CommandBattleCore
{
    // ステータスエフェクトが「いつ切れるか」を表す持続条件
    // ターン数で切れるのか、永続なのか、何らかの条件式で切れるのかをエフェクト本体から切り離してある
    // BattleUnit.TickStatusEffects が毎更新で Tick を呼び、false が返った時点でエフェクトを除去する
    public interface IDurationCondition
    {
        // 更新のたびに呼ばれる
        // ターン制バトルならターン毎、ATB なら一定時間毎など
        // return : 効果を継続する場合 true。false を返すとエフェクトが除去される
        bool Tick();
    }

    // 継続時間を延長し直せる持続条件であることを示すインターフェース
    // StatusEffectStackPolicy.Refresh 系の重ね掛けで使われる
    public interface IRefreshableDuration
    {
        // 継続時間を初期値へ戻す
        void Refresh();
    }

    // 指定ターン数が経過すると切れる持続条件
    // Tick は減算してから判定するため、残り 1 の状態で呼ばれるとその場で終了する
    // 付与直後にも Tick が回る前提の呼び出し順になっている点に注意
    // （BattleSkill.NotifyUsed がクールダウンを +1 しているのと同じ理由）
    public sealed class TurnDurationCondition : IDurationCondition, IRefreshableDuration
    {
        // リフレッシュ時に戻す初期ターン数
        private readonly int mInitialDuration;
        // 残りターン数
        public int RemainingDuration {get; private set;}

        // aDuration : 持続ターン数
        public TurnDurationCondition(int aDuration)
        {
            mInitialDuration = aDuration;
            RemainingDuration = aDuration;
        }

        // 残りターン数を 1 減らし、まだ残っていれば継続を返す
        public bool Tick() => --RemainingDuration > 0;
        // 残りターン数を初期値へ戻す
        public void Refresh() => RemainingDuration = mInitialDuration;  // 初期ターン数に復帰する
    }

    // 自然には切れない持続条件。解除するにはアイテムや解除スキルで明示的に除去する必要がある
    public sealed class PermanentDurationCondition : IDurationCondition
    {
        // 常に継続を返す
        public bool Tick() => true;
    }

    // 任意の条件式で継続可否を制御する汎用実装
    // 「HP が一定以下の間だけ」といった、ターン数で表せない持続条件に使う
    public sealed class PredicateDurationCondition : IDurationCondition
    {
        // 継続すべきかを判定する関数
        private readonly System.Func<bool> mShouldContinueFunc;

        // aShouldContinueFunc : 継続する間 true を返す関数
        public PredicateDurationCondition(System.Func<bool> aShouldContinueFunc) => mShouldContinueFunc = aShouldContinueFunc;

        // 条件式を評価してそのまま返す
        public bool Tick() => mShouldContinueFunc();
    }
}
