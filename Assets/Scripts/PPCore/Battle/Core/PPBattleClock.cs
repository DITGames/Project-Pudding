/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleClock.cs
 * @author hqrse
 * @date 2026/11/16
 * @brief バトル専用の時計と、秒指定で処理を予約するタイマー
 * =====================================*/

using System;
using System.Collections.Generic;
using CustomConsole;

namespace PPCore
{
    // バトル専用の時計。停止中は進まず、状態異常など秒で決まる処理の基準になる
    public sealed class PPBattleClock
    {
        /* 状態 */

        // バトル開始からの経過秒。停止していた時間は含まない
        public float Now { get; private set; }
        // 停止の要求数。入力と演出など複数の要求が重なっても、全て解除されるまで止めておく
        private int mPauseCount;

        /* プロパティ */

        public bool IsPaused => mPauseCount > 0;

        /* イベント */

        // 停止と再開が切り替わったとき(停止中なら true)
        public event Action<bool> OnPauseChanged;

        /* 操作 */

        // 時計を止める。呼んだ回数だけ Resume を呼ぶまで再開しない
        public void Pause()
        {
            mPauseCount++;
            if (mPauseCount == 1) OnPauseChanged?.Invoke(true);
        }

        // 停止の要求を 1 つ解除する。要求が残っていなければ再開する
        public void Resume()
        {
            if (mPauseCount == 0) return;

            mPauseCount--;
            if (mPauseCount == 0) OnPauseChanged?.Invoke(false);
        }

        // 時計を進め、実際に進めた秒数を返す。停止中は進めず 0 を返す
        public float Advance(float aDeltaTime)
        {
            if (IsPaused || aDeltaTime <= 0f) return 0f;

            Now += aDeltaTime;
            return aDeltaTime;
        }
    }

    // 予約した処理 1 件分。取り消しと残り時間の問い合わせに使う
    public sealed class PPBattleTimer
    {
        // 発火する時刻（バトル専用の時計の秒）
        public float DueTime { get; internal set; }
        // 発火も取り消しもされておらず、まだ待っているか
        public bool IsPending { get; internal set; }
        internal Action Callback;
    }

    // バトル専用の時計の時刻で、処理を秒指定で予約・取り消し・発火する
    public sealed class PPBattleTimerScheduler
    {
        /* 定数 */

        // 1 回の進行で発火できる上限。遅延 0 の予約を繰り返す処理で固まらないための歯止め
        private const int MaxFiresPerAdvance = 1000;
        // 発火時刻の比較に使う許容誤差。秒の加算を繰り返した誤差で、ちょうどの時刻の発火が次の進行へずれないようにする
        private const float CompareEpsilon = 0.0001f;

        /* 状態 */

        // 待っているタイマー。発火時刻の昇順で、同時刻は予約した順
        private readonly List<PPBattleTimer> mTimers = new();
        // 予約の基準にする時刻。発火中は発火したタイマーの予定時刻にし、繰り返しの予約がずれないようにする
        private float mBaseTime;

        /* プロパティ */

        public int PendingCount => mTimers.Count;

        /* 操作 */

        // 指定秒後に 1 回だけ実行する処理を予約する。発火中に呼ぶと、発火したタイマーの予定時刻が基準になる
        public PPBattleTimer Schedule(float aDelay, Action aCallback)
        {
            if (aCallback == null) throw new ArgumentNullException(nameof(aCallback));

            var timer = new PPBattleTimer
            {
                DueTime = mBaseTime + Math.Max(0f, aDelay),
                IsPending = true,
                Callback = aCallback,
            };

            // 同時刻のタイマーの後ろへ入れ、予約した順に発火させる
            int index = mTimers.Count;
            while (index > 0 && mTimers[index - 1].DueTime > timer.DueTime) index--;
            mTimers.Insert(index, timer);
            return timer;
        }

        // 予約を取り消す。発火済み・取り消し済みなら何もせず false を返す
        public bool Cancel(PPBattleTimer aTimer)
        {
            if (aTimer == null || !aTimer.IsPending) return false;

            aTimer.IsPending = false;
            aTimer.Callback = null;
            return mTimers.Remove(aTimer);
        }

        // 待っている予約を全て取り消す
        public void CancelAll()
        {
            foreach (var timer in mTimers)
            {
                timer.IsPending = false;
                timer.Callback = null;
            }
            mTimers.Clear();
        }

        // 指定時刻までに期限が来たタイマーを、発火時刻の順に実行する
        public void AdvanceTo(float aNow)
        {
            int fired = 0;
            while (mTimers.Count > 0 && mTimers[0].DueTime <= aNow + CompareEpsilon)
            {
                if (++fired > MaxFiresPerAdvance)
                {
                    CustomConsoleLog.Error("Battle",
                        $"タイマーの発火が 1 回の進行で {MaxFiresPerAdvance} 件を超えたため打ち切りました。遅延 0 の予約が繰り返されていないか確認してください。");
                    break;
                }

                // 処理の中での取り消しや再予約に備え、実行前にリストから外しておく
                var timer = mTimers[0];
                mTimers.RemoveAt(0);
                var callback = timer.Callback;
                timer.IsPending = false;
                timer.Callback = null;

                mBaseTime = timer.DueTime;
                callback();
            }

            mBaseTime = aNow;
        }

        /* 問い合わせ */

        // 発火までの残り秒を返す。待っていないタイマーは 0
        public float GetRemaining(PPBattleTimer aTimer)
            => aTimer != null && aTimer.IsPending ? Math.Max(0f, aTimer.DueTime - mBaseTime) : 0f;
    }
}
