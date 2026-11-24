/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveRunner.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief ウェーブの進行(段階の開始・敵の出現)と、マスの深さによるレベルの割り振り
 * =====================================*/

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

    // バトル 1 回分のウェーブの進行。段階を順に待ち、始まった段階から敵拠点の近くへ敵を出す
    // バトル専用の時計で進めるため、メニューを開いている間は止まる
    public class PPWaveRunner
    {
        /* 内部型 */

        // 始まった段階 1 回分。繰り返しで同じ段階がまた始まったら、別の回として作る
        private class StepRun
        {
            public readonly int StepIndex;
            public readonly SpawnRun[] Spawns;
            public readonly List<BattleUnit> SpawnedUnits = new();

            public StepRun(int aStepIndex, PPWaveStep aStep, float aStartTime)
            {
                StepIndex = aStepIndex;
                Spawns = new SpawnRun[aStep.Spawns.Count];
                for (int i = 0; i < Spawns.Length; i++) Spawns[i] = new SpawnRun(aStep.Spawns[i], aStartTime);
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

        // 1 種類の敵を出す進み具合
        private class SpawnRun
        {
            public readonly PPWaveSpawn Spawn;
            public float NextTime;
            public int SpawnedCount;

            public SpawnRun(PPWaveSpawn aSpawn, float aStartTime)
            {
                Spawn = aSpawn;
                NextTime = aStartTime + aSpawn.FirstDelay;
            }

            public bool IsFinished => Spawn.Unit == null || (!Spawn.IsInfinite && SpawnedCount >= Spawn.Count);
        }

        /* 定数 */

        private const string LogTag = "Battle";

        /* 状態 */

        private readonly PPWaveDefinition mWave;
        private readonly BattleManager mManager;
        private readonly float mSpawnPosition;
        private readonly Dictionary<PPUnitDefinition, int> mLevelBonus;
        private readonly List<StepRun> mRuns = new();
        // 段階ごとの最新の回。全滅の判定に使う
        private readonly Dictionary<int, StepRun> mLatestRuns = new();
        private readonly PPWaveTriggerContext mTriggerContext;
        // このバトル中に段階を始めた条件。「1 度だけ」の条件の判定に使う
        private readonly HashSet<PPWaveTrigger> mFiredTriggers = new();
        // 次に始める段階。-1 なら、もう始める段階は無い
        private int mNextStep;
        private int mAllyBaseDamageCount;
        private int mEnemyBaseDamageCount;

        /* プロパティ */

        // マスの深さで割り振られた、ユニットの種類ごとの追加のレベル
        public IReadOnlyDictionary<PPUnitDefinition, int> LevelBonus => mLevelBonus;
        // 次に始める段階(0 から数える)。-1 なら、もう始める段階は無い
        public int NextStepIndex => mNextStep;
        // これまでに始めた段階の回数(繰り返しで同じ段階を始めた分も数える)
        public int StartedStepCount => mRuns.Count;

        /* 生成 */

        // aSpawnPosition は敵を出す道の位置(味方拠点からの距離)。aLevelPoints は深さで割り振るレベル
        public PPWaveRunner(PPWaveDefinition aWave, BattleManager aManager, float aSpawnPosition, int aLevelPoints)
        {
            mWave = aWave;
            mManager = aManager;
            mSpawnPosition = aSpawnPosition;
            mLevelBonus = PPWaveLevelAllocator.Allocate(aWave.CollectUnits(), aLevelPoints, aManager.Context.Rules.RandomProvider);
            mTriggerContext = new PPWaveTriggerContext(IsStepWiped, GetBaseDamageCount, GetBaseHpRatio, mFiredTriggers.Contains);
            mNextStep = aWave.Steps.Count > 0 ? 0 : -1;
            Arm(aManager.Context.Clock.Now);

            aManager.OnBaseDamaged += (fieldBase, _) =>
            {
                if (fieldBase.Side == BattleSide.Ally) mAllyBaseDamageCount++;
                else mEnemyBaseDamageCount++;
            };
        }

        /* 操作 */

        // 始まる条件を満たした段階を始め、始まっている段階から時刻の来た敵を出す。決着後は何もしない
        public void Update()
        {
            if (mManager.Context == null || mManager.StateMachine.Current == BattleState.BattleEnd) return;
            float now = mManager.Context.Clock.Now;

            // 条件の無い段階が続いても 1 フレームで回り続けないよう、1 フレームに始めるのは 1 つまで
            TryStartNextStep(now);
            foreach (var run in mRuns) UpdateSpawns(run, now);
        }

        // 待っている段階の条件を満たしていれば始め、次に待つ段階へ進める
        private void TryStartNextStep(float aNow)
        {
            if (mNextStep < 0) return;
            var step = mWave.Steps[mNextStep];
            mTriggerContext.Now = aNow;
            if (step == null || !step.CanStart(mTriggerContext, out var satisfied)) return;
            if (satisfied != null) mFiredTriggers.Add(satisfied);

            var run = new StepRun(mNextStep, step, aNow);
            mRuns.Add(run);
            mLatestRuns[mNextStep] = run;
            CustomConsoleLog.Log(LogTag, $"ウェーブ: 段階 {mNextStep + 1}「{step.Memo}」を始める(時刻 {aNow:F1} 条件={(satisfied != null ? satisfied.GetType().Name : "なし")})");

            mNextStep = GetFollowingStep(mNextStep);
            Arm(aNow);
        }

        // 時刻の来た敵を出す。1 フレームの遅れでまとめて出さないよう、出すのは 1 フレームに 1 体まで
        private void UpdateSpawns(StepRun aRun, float aNow)
        {
            foreach (var spawn in aRun.Spawns)
            {
                if (spawn.IsFinished || aNow < spawn.NextTime) continue;

                var definition = spawn.Spawn.Unit;
                int level = spawn.Spawn.Level + mLevelBonus.GetValueOrDefault(definition);
                var unit = definition.CreateRuntimeUnit(level);
                spawn.SpawnedCount++;
                spawn.NextTime = Mathf.Max(spawn.NextTime + spawn.Spawn.Interval, aNow);
                if (mManager.SpawnFieldUnit(unit, BattleSide.Enemy, mSpawnPosition) != null) aRun.SpawnedUnits.Add(unit);
            }
        }

        // aStep の次に待つ段階。繰り返す場合は、繰り返しの終わりの次に始まりへ戻る
        private int GetFollowingStep(int aStep)
        {
            if (mWave.IsLoop && aStep == mWave.LoopEndIndex) return mWave.LoopStartIndex;
            int next = aStep + 1;
            return next < mWave.Steps.Count ? next : -1;
        }

        // 次の段階が待ち始めた時点の時刻と、拠点がダメージを受けた回数を覚える
        private void Arm(float aNow)
        {
            mTriggerContext.ArmedTime = aNow;
            mTriggerContext.AllyDamageCountAtArm = mAllyBaseDamageCount;
            mTriggerContext.EnemyDamageCountAtArm = mEnemyBaseDamageCount;
        }

        /* 問い合わせ */

        private bool IsStepWiped(int aStepIndex) => mLatestRuns.TryGetValue(aStepIndex, out var run) && run.IsWiped;

        private int GetBaseDamageCount(BattleSide aSide) => aSide == BattleSide.Ally ? mAllyBaseDamageCount : mEnemyBaseDamageCount;

        private float GetBaseHpRatio(BattleSide aSide)
        {
            var fieldBase = mManager.Context?.Field?.GetBase(aSide);
            return fieldBase != null ? fieldBase.Unit.Parameters.Hp.Ratio : 1f;
        }
    }
}
