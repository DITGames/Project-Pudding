/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IDurationCondition.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 状態異常の持続条件
 * =====================================*/
using UnityEngine;

namespace CommandBattleCore
{
    public interface IDurationCondition
    {
        // 更新のたびに呼ばれる
        // ターン制バトルならターン毎
        // ATBなら一定時間毎など...
        bool Tick();
    }

    public interface IRefreshableDuration
    {
        void Refresh();
    }

    // 指定期間で切れる持続条件
    public sealed class TurnDurationCondition : IDurationCondition, IRefreshableDuration
    {
        private readonly int mInitialDuration;
        public int RemainingDuration {get; private set;}
        public TurnDurationCondition(int aDuration)
        {
            mInitialDuration = aDuration;
            RemainingDuration = aDuration;
        }
        public bool Tick() => --RemainingDuration > 0;
        public void Refresh() => RemainingDuration = mInitialDuration;  // 初期ターン数に復帰する
    }

    // 永続(アイテムの使用で消えるなどはある)
    public sealed class PermanentDurationCondition : IDurationCondition
    {
        public bool Tick() => true;
    }

    // 任意の条件式で制御する汎用実装
    public sealed class PredicateDurationCondition : IDurationCondition
    {
        private readonly System.Func<bool> mShouldContinueFunc;
        public PredicateDurationCondition(System.Func<bool> aShouldContinueFunc) => mShouldContinueFunc = aShouldContinueFunc;
        public bool Tick() => mShouldContinueFunc();
    }
}