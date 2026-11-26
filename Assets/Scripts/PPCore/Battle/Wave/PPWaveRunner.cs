/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveRunner.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief ウェーブの進行(段階の待ち・始まり・行き先に沿った敵の出現)と、マスの深さによるレベルの割り振り
 * =====================================*/

using System;
using System.Collections.Generic;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // マスの深さで決まるレベルを、出てくる敵の種類へ 1 レベルずつ無作為に割り振る
    public static class PPWaveLevelAllocator
    {
        /* 操作 */

        // aPoints レベルを aUnits へ割り振る。1 種類に偏ることも、種類ごとに散ることもある
        public static Dictionary<PPUnitDefinition, int> Allocate(IReadOnlyList<PPUnitDefinition> aUnits, int aPoints, IRandomProvider aRandom)
        {
            var bonus = new Dictionary<PPUnitDefinition, int>();
            foreach (var unit in aUnits) bonus[unit] = 0;
            if (aUnits.Count == 0) return bonus;

            for (int i = 0; i < aPoints; i++)
            {
                var unit = aUnits[aRandom.NextInt(aUnits.Count)];
                bonus[unit]++;
            }
            return bonus;
        }
    }

    // 段階 1 つの、今の実行の様子。実行中の表示や検証で読む
    public readonly struct PPWaveStageStatus
    {
        /* プロパティ */

        public bool IsWaiting { get; }
        // 始めた回数
        public int StartCount { get; }
        // 出している最中の回の数
        public int RunningCount { get; }
        // これまでに出した敵の数(すべての回の合計)
        public int SpawnedCount { get; }
        // 最後に始めたときに満たした条件の番号(0 から)。条件なしで始めたら -1、まだ始めていなければ -2
        public int LastConditionIndex { get; }

        /* 生成 */

        public PPWaveStageStatus(bool aIsWaiting, int aStartCount, int aRunningCount, int aSpawnedCount, int aLastConditionIndex)
        {
            IsWaiting = aIsWaiting;
            StartCount = aStartCount;
            RunningCount = aRunningCount;
            SpawnedCount = aSpawnedCount;
            LastConditionIndex = aLastConditionIndex;
        }
    }

    // バトル 1 回分のウェーブの進行。待っている段階を条件で始め、出し始め・出し終え・全滅で行き先の段階を待ち始めさせる
    // バトル専用の時計で進めるため、メニューを開いている間は止まる
    public class PPWaveRunner
    {
        /* 内部型 */

        // 段階 1 つ分の実行中の状態
        private class StageState
        {
            public readonly PPWaveStage Stage;
            public bool IsWaiting;
            // 待ち始めた時刻と、待ち始めた時点で各拠点がダメージを受けていた回数
            public float ArmedTime;
            public int AllyDamageCountAtArm;
            public int EnemyDamageCountAtArm;
            // 条件ごとの「1 度だけ」を使ったか
            public readonly bool[] IsConditionUsed;
            // 条件ごとの、最後に始めるきっかけにした全滅の回の番号。同じ全滅では 2 度始めない
            public readonly int[] LastWipedRunNumber;
            public int StartCount;
            public int RunningCount;
            public int SpawnedCount;
            public int LastConditionIndex = NotStarted;
            public SpawnRun LatestRun;
            public int StartedFrame = -1;
            // 続けて始めたフレーム数。待ちのない繰り返しの警告に使う
            public int StartStreak;
            public bool IsLoopWarned;

            public StageState(PPWaveStage aStage)
            {
                Stage = aStage;
                IsConditionUsed = new bool[aStage.StartConditions.Count];
                LastWipedRunNumber = new int[aStage.StartConditions.Count];
            }
        }

        // 段階の 1 回分。同じ段階がまた始まったら、別の回として作る
        private class SpawnRun
        {
            public readonly StageState Owner;
            public readonly int Number;
            public readonly SpawnEntry[] Spawns;
            public readonly List<BattleUnit> SpawnedUnits = new();
            public bool IsFinishedNotified;
            public bool IsWipedNotified;

            public SpawnRun(StageState aOwner, int aNumber, float aStartTime)
            {
                Owner = aOwner;
                Number = aNumber;
                var spawns = aOwner.Stage.Spawns;
                Spawns = new SpawnEntry[spawns.Count];
                for (int i = 0; i < Spawns.Length; i++) Spawns[i] = new SpawnEntry(spawns[i], i, aStartTime);
            }

            public bool IsFinished
            {
                get
                {
                    foreach (var spawn in Spawns)
                    {
                        if (!spawn.IsFinished) return false;
                    }
                    return true;
                }
            }

            // 出し終えていて、出した敵が 1 体以上いて、すべて倒されているか
            public bool IsWiped
            {
                get
                {
                    if (!IsFinished || SpawnedUnits.Count == 0) return false;
                    foreach (var unit in SpawnedUnits)
                    {
                        if (unit.IsAlive) return false;
                    }
                    return true;
                }
            }
        }

        // 表の 1 行の敵を出す進み具合
        private class SpawnEntry
        {
            public readonly PPWaveSpawn Spawn;
            public readonly int Row;
            public float NextTime;
            public int SpawnedCount;

            public SpawnEntry(PPWaveSpawn aSpawn, int aRow, float aStartTime)
            {
                Spawn = aSpawn;
                Row = aRow;
                NextTime = aStartTime + (aSpawn?.FirstDelay ?? 0f);
            }

            public bool IsFinished => Spawn?.Unit == null || (!Spawn.IsInfinite && SpawnedCount >= Spawn.Count);
        }

        /* 定数 */

        // LastConditionIndex の値。条件なしで始めた / まだ始めていない
        public const int StartedWithoutCondition = -1;
        public const int NotStarted = -2;
        private const string LogTag = "Battle";
        private const float CompareEpsilon = 0.0001f;
        // 同じ段階が続けてこのフレーム数だけ始まったら、待ちのない繰り返しとして警告する
        private const int LoopWarnFrames = 10;

        /* 状態 */

        private readonly PPWaveDefinition mWave;
        private readonly BattleManager mManager;
        private readonly float mSpawnPosition;
        private readonly Dictionary<PPUnitDefinition, int> mLevelBonus;
        // 段階の状態。定義の並び順
        private readonly List<StageState> mStates = new();
        private readonly Dictionary<string, StageState> mStatesById = new();
        // 出している最中か、まだ全滅し得る回
        private readonly List<SpawnRun> mRuns = new();
        private int mFrame;
        private int mAllyBaseDamageCount;
        private int mEnemyBaseDamageCount;

        /* プロパティ */

        public PPWaveDefinition Definition => mWave;
        // マスの深さで割り振られた、ユニットの種類ごとの追加のレベル
        public IReadOnlyDictionary<PPUnitDefinition, int> LevelBonus => mLevelBonus;
        public float Now => mManager.Context?.Clock.Now ?? 0f;

        /* イベント */

        // 段階が待ち始めたとき(段階, 行き先にした段階。バトル開始なら null)
        public event Action<PPWaveStage, PPWaveStage> OnStageArmed;
        // 段階が始まったとき(段階, 満たした条件の番号。条件なしなら -1, 回の番号)
        public event Action<PPWaveStage, int, int> OnStageStarted;
        // 段階の回が出し終えたとき(段階, 回の番号)
        public event Action<PPWaveStage, int> OnStageFinished;
        // 段階の回が出し終えたうえで全滅したとき(段階, 回の番号)
        public event Action<PPWaveStage, int> OnStageWiped;
        // 段階が敵を出したとき(段階, 表の行の番号, 出したユニット)
        public event Action<PPWaveStage, int, BattleUnit> OnEnemySpawned;

        /* 生成 */

        // aSpawnPosition は敵を出す道の位置(味方拠点からの距離)。aLevelPoints は深さで割り振るレベル
        public PPWaveRunner(PPWaveDefinition aWave, BattleManager aManager, float aSpawnPosition, int aLevelPoints)
        {
            mWave = aWave;
            mManager = aManager;
            mSpawnPosition = aSpawnPosition;
            mLevelBonus = PPWaveLevelAllocator.Allocate(aWave.CollectUnits(), aLevelPoints, aManager.Context.Rules.RandomProvider);
            BuildStates();

            foreach (var state in mStates)
            {
                if (state.Stage.WaitsFromBattleStart) Arm(state, null);
            }

            aManager.OnBaseDamaged += (fieldBase, _) =>
            {
                if (fieldBase.Side == BattleSide.Ally) mAllyBaseDamageCount++;
                else mEnemyBaseDamageCount++;
            };
        }

        private void BuildStates()
        {
            var stages = mWave.Stages;
            for (int i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (stage == null) continue;
                // 識別子が空・重なった段階は、行き先が取り違えられるため使わない
                if (string.IsNullOrEmpty(stage.Id) || mStatesById.ContainsKey(stage.Id))
                {
                    CustomConsoleLog.Warning(LogTag, $"ウェーブ: 段階 {i + 1}「{stage.Name}」の識別子が空か他と重なっているため、使いません。ウェーブエディタで段階を複製し直してください");
                    continue;
                }
                var state = new StageState(stage);
                mStates.Add(state);
                mStatesById[stage.Id] = state;
            }
        }

        /* 操作 */

        // 待っている段階を条件で始め、始まっている回から時刻の来た敵を出し、行き先を待ち始めさせる。決着後は何もしない
        public void Update()
        {
            if (mManager.Context == null || mManager.StateMachine.Current == BattleState.BattleEnd) return;
            mFrame++;
            float now = Now;
            StartSatisfiedStages(now);
            UpdateRuns(now);
            StartSatisfiedStages(now);
        }

        // 待っている段階を定義の並び順に判定し、条件を満たしたものを始める。始めた段階が待ち始めさせた段階も、同じフレームのうちに判定する
        // 1 つの段階は 1 フレームに 1 回までしか始めないため、待ちのない繰り返しでも止まる
        private void StartSatisfiedStages(float aNow)
        {
            bool isStarted;
            do
            {
                isStarted = false;
                foreach (var state in mStates)
                {
                    if (!state.IsWaiting || state.StartedFrame == mFrame) continue;
                    int condition = FindSatisfiedCondition(state);
                    if (condition == NotStarted) continue;
                    Start(state, condition, aNow);
                    isStarted = true;
                }
            }
            while (isStarted);
        }

        // 段階を待ち始めさせ、待ち始めた時刻と拠点の被ダメージ回数を覚える。待っている途中なら、待ち始めをやり直す
        private void Arm(StageState aState, StageState aSource)
        {
            aState.IsWaiting = true;
            aState.ArmedTime = Now;
            aState.AllyDamageCountAtArm = mAllyBaseDamageCount;
            aState.EnemyDamageCountAtArm = mEnemyBaseDamageCount;
            OnStageArmed?.Invoke(aState.Stage, aSource?.Stage);
        }

        // 段階の新しい回を始め、「出し始めたら」の行き先へ進む
        private void Start(StageState aState, int aConditionIndex, float aNow)
        {
            aState.IsWaiting = false;
            if (aConditionIndex >= 0)
            {
                var condition = aState.Stage.StartConditions[aConditionIndex];
                if (condition.IsOnce) aState.IsConditionUsed[aConditionIndex] = true;
                if (condition.Kind == PPWaveStartKind.StageWiped && mStatesById.TryGetValue(condition.StageId ?? string.Empty, out var target) && target.LatestRun != null)
                {
                    aState.LastWipedRunNumber[aConditionIndex] = target.LatestRun.Number;
                }
            }
            WarnIfLooping(aState);
            aState.StartedFrame = mFrame;
            aState.StartCount++;
            aState.RunningCount++;
            aState.LastConditionIndex = aConditionIndex;
            var run = new SpawnRun(aState, aState.StartCount, aNow);
            aState.LatestRun = run;
            mRuns.Add(run);

            string cause = aConditionIndex >= 0 ? aState.Stage.StartConditions[aConditionIndex].Describe(mWave, aState.Stage, false) : "条件なし";
            CustomConsoleLog.Log(LogTag, $"ウェーブ: 「{GetName(aState)}」を出し始める(時刻 {aNow:F1} {run.Number} 回目 きっかけ={cause})");
            OnStageStarted?.Invoke(aState.Stage, aConditionIndex, run.Number);
            ApplyNexts(aState, PPWaveNextTiming.Started);
        }

        // 同じ段階が続けてフレームごとに始まっていたら、待ちのない繰り返しとして 1 度だけ警告する
        private void WarnIfLooping(StageState aState)
        {
            aState.StartStreak = aState.StartedFrame == mFrame - 1 ? aState.StartStreak + 1 : 1;
            if (aState.IsLoopWarned || aState.StartStreak < LoopWarnFrames) return;
            aState.IsLoopWarned = true;
            CustomConsoleLog.Warning(LogTag, $"ウェーブ: 段階「{GetName(aState)}」が 1 フレームに 1 回ずつ始まり続けています。待ちのない繰り返しになっていないか、バトル定義のウェーブを確かめてください");
        }

        // 始まっている回から時刻の来た敵を出し、出し終えた・全滅した回の行き先へ進む
        // 1 フレームの遅れでまとめて出さないよう、出すのは 1 行につき 1 フレームに 1 体まで
        private void UpdateRuns(float aNow)
        {
            int kept = 0;
            for (int i = 0; i < mRuns.Count; i++)
            {
                var run = mRuns[i];
                foreach (var spawn in run.Spawns)
                {
                    if (spawn.IsFinished || aNow < spawn.NextTime) continue;

                    var definition = spawn.Spawn.Unit;
                    var unit = definition.CreateRuntimeUnit(1 + mLevelBonus.GetValueOrDefault(definition));
                    spawn.SpawnedCount++;
                    spawn.NextTime = Mathf.Max(spawn.NextTime + spawn.Spawn.Interval, aNow);
                    if (mManager.SpawnFieldUnit(unit, BattleSide.Enemy, mSpawnPosition) == null) continue;
                    run.SpawnedUnits.Add(unit);
                    run.Owner.SpawnedCount++;
                    OnEnemySpawned?.Invoke(run.Owner.Stage, spawn.Row, unit);
                }

                if (!run.IsFinishedNotified && run.IsFinished)
                {
                    run.IsFinishedNotified = true;
                    run.Owner.RunningCount--;
                    OnStageFinished?.Invoke(run.Owner.Stage, run.Number);
                    ApplyNexts(run.Owner, PPWaveNextTiming.Finished);
                }
                if (!run.IsWipedNotified && run.IsWiped)
                {
                    run.IsWipedNotified = true;
                    OnStageWiped?.Invoke(run.Owner.Stage, run.Number);
                    ApplyNexts(run.Owner, PPWaveNextTiming.Wiped);
                }
                // 全滅を知らせた回と、1 体も出さずに出し終えた回は、もう見る必要がない
                if (run.IsWipedNotified || (run.IsFinishedNotified && run.SpawnedUnits.Count == 0)) continue;
                mRuns[kept++] = run;
            }
            mRuns.RemoveRange(kept, mRuns.Count - kept);
        }

        // 段階のそのときの行き先のうち、条件が成り立つものを待ち始めさせる
        private void ApplyNexts(StageState aState, PPWaveNextTiming aTiming)
        {
            foreach (var next in aState.Stage.Nexts)
            {
                if (next == null || next.Timing != aTiming) continue;
                if (!mStatesById.TryGetValue(next.TargetId ?? string.Empty, out var target)) continue;
                if (!next.IsConditionMet(GetBaseHpRatio(next.Side)))
                {
                    CustomConsoleLog.Verbose(LogTag, $"ウェーブ: 「{GetName(aState)}」の行き先「{GetName(target)}」は条件が成り立たないため進まない(時刻 {Now:F1})");
                    continue;
                }
                Arm(target, aState);
            }
        }

        /* 評価・判定 */

        // 満たした条件の番号を返す。条件が無ければ StartedWithoutCondition、どれも満たさなければ NotStarted
        private int FindSatisfiedCondition(StageState aState)
        {
            var conditions = aState.Stage.StartConditions;
            if (conditions.Count == 0) return StartedWithoutCondition;
            for (int i = 0; i < conditions.Count; i++)
            {
                if (IsConditionSatisfied(aState, i)) return i;
            }
            return NotStarted;
        }

        // 待っている段階の条件 1 つが、今成り立つか。「1 度だけ」を使った条件は成り立たない
        protected virtual bool IsConditionSatisfied(PPWaveStartCondition aCondition, float aArmedTime, int aDamageCountSinceArmed, int aLastWipedRunNumber)
        {
            switch (aCondition.Kind)
            {
                case PPWaveStartKind.Elapsed:
                    return Now - aArmedTime >= aCondition.Seconds - CompareEpsilon;
                case PPWaveStartKind.StageWiped:
                    return mStatesById.TryGetValue(aCondition.StageId ?? string.Empty, out var target) && target.LatestRun != null
                        && target.LatestRun.IsWiped && target.LatestRun.Number != aLastWipedRunNumber;
                case PPWaveStartKind.BaseDamaged:
                    return aDamageCountSinceArmed > 0;
                case PPWaveStartKind.BaseHp:
                    return GetBaseHpRatio(aCondition.Side) <= aCondition.Percent / 100f + CompareEpsilon;
                default:
                    return false;
            }
        }

        private bool IsConditionSatisfied(StageState aState, int aIndex)
        {
            if (aState.IsConditionUsed[aIndex]) return false;
            var condition = aState.Stage.StartConditions[aIndex];
            int damageCount = condition.Side == BattleSide.Ally ? mAllyBaseDamageCount - aState.AllyDamageCountAtArm : mEnemyBaseDamageCount - aState.EnemyDamageCountAtArm;
            return IsConditionSatisfied(condition, aState.ArmedTime, damageCount, aState.LastWipedRunNumber[aIndex]);
        }

        /* 問い合わせ */

        // 識別子の段階の今の様子。見つからなければ、待っていない・始めていない様子
        public PPWaveStageStatus GetStatus(string aStageId)
        {
            if (string.IsNullOrEmpty(aStageId) || !mStatesById.TryGetValue(aStageId, out var state)) return new PPWaveStageStatus(false, 0, 0, 0, NotStarted);
            return new PPWaveStageStatus(state.IsWaiting, state.StartCount, state.RunningCount, state.SpawnedCount, state.LastConditionIndex);
        }

        // 識別子の段階の条件(番号は 0 から)が、最後に待ち始めた時点から見て今成り立つか
        public bool IsConditionSatisfied(string aStageId, int aIndex)
        {
            if (string.IsNullOrEmpty(aStageId) || !mStatesById.TryGetValue(aStageId, out var state)) return false;
            return aIndex >= 0 && aIndex < state.Stage.StartConditions.Count && IsConditionSatisfied(state, aIndex);
        }

        // その陣営の拠点の HP の割合(0〜1)。拠点が無ければ 1
        public float GetBaseHpRatio(BattleSide aSide)
        {
            var fieldBase = mManager.Context?.Field?.GetBase(aSide);
            return fieldBase != null ? fieldBase.Unit.Parameters.Hp.Ratio : 1f;
        }

        /* ヘルパー */

        private string GetName(StageState aState) => mWave.GetStageName(aState.Stage.Id);
    }
}
