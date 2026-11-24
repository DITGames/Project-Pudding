/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveDefinition.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief ウェーブの定義(段階・敵の出し方・段階が始まる条件・繰り返し区間)
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // 段階の中で 1 種類の敵を出す設定。始まってから「最初の待ち時間」後に 1 体目を出し、以降は間隔ごとに出す
    [Serializable]
    public class PPWaveSpawn
    {
        /* インスペクタ設定 */

        [Label("ユニット")]
        [SerializeField] private PPUnitDefinition mUnit;
        // マスの深さで割り振られたレベルは、ここに足される
        [Label("レベル")]
        [Min(1)]
        [SerializeField] private int mLevel = 1;
        // 0 なら、バトルが終わるまで出し続ける
        [Label("数(0 で無限)")]
        [Min(0)]
        [SerializeField] private int mCount = 5;
        [Label("間隔(秒)")]
        [Min(0.1f)]
        [SerializeField] private float mInterval = 3f;
        [Label("最初の待ち時間(秒)")]
        [Min(0f)]
        [SerializeField] private float mFirstDelay = 0f;

        /* プロパティ */

        public PPUnitDefinition Unit => mUnit;
        public int Level => Mathf.Max(1, mLevel);
        public int Count => Mathf.Max(0, mCount);
        public bool IsInfinite => mCount <= 0;
        public float Interval => Mathf.Max(0.1f, mInterval);
        public float FirstDelay => Mathf.Max(0f, mFirstDelay);

        /* 生成 */

        public PPWaveSpawn() { }

        public PPWaveSpawn(PPUnitDefinition aUnit, int aLevel, int aCount, float aInterval, float aFirstDelay = 0f)
        {
            mUnit = aUnit;
            mLevel = aLevel;
            mCount = aCount;
            mInterval = aInterval;
            mFirstDelay = aFirstDelay;
        }
    }

    // ウェーブの段階 1 つ。前の段階が始まったら待ち始め、始まる条件のどれかを満たしたら敵を出し始める
    // 次の段階が始まっても、この段階は出し終えるまで(無限なら決着まで)出し続ける
    [Serializable]
    public class PPWaveStep
    {
        /* インスペクタ設定 */

        // インスペクタで見分けるための名前。動きには使わない
        [Label("メモ")]
        [SerializeField] private string mMemo;
        // 空なら、待ち始めたらすぐに始まる
        [SerializeReference]
        [Label("始まる条件(どれか 1 つ)", true)]
        private List<PPWaveTrigger> mConditions = new();
        [Label("出す敵", true)]
        [SerializeField] private List<PPWaveSpawn> mSpawns = new();

        /* プロパティ */

        public string Memo => mMemo;
        public IReadOnlyList<PPWaveTrigger> Conditions => mConditions;
        public IReadOnlyList<PPWaveSpawn> Spawns => mSpawns;

        /* 生成 */

        public PPWaveStep() { }

        public PPWaveStep(string aMemo, IEnumerable<PPWaveTrigger> aConditions, IEnumerable<PPWaveSpawn> aSpawns)
        {
            mMemo = aMemo;
            mConditions = new List<PPWaveTrigger>(aConditions);
            mSpawns = new List<PPWaveSpawn>(aSpawns);
        }

        /* 評価・判定 */

        // 条件が空ならすぐ始め、そうでなければどれか 1 つを満たしたら始める。満たした条件を aSatisfied に返す(条件が空なら null)
        public bool CanStart(PPWaveTriggerContext aContext, out PPWaveTrigger aSatisfied)
        {
            aSatisfied = null;
            if (mConditions.Count == 0) return true;
            foreach (var condition in mConditions)
            {
                if (condition == null || !condition.IsSatisfied(aContext)) continue;
                aSatisfied = condition;
                return true;
            }
            return false;
        }
    }

    // ステージ定義が持つウェーブ。段階を上から順に始め、繰り返す場合は終わりの段階の次に始まりの段階へ戻る
    [Serializable]
    public class PPWaveDefinition
    {
        /* インスペクタ設定 */

        [Label("段階", true)]
        [SerializeField] private List<PPWaveStep> mSteps = new();
        // 最後の段階の後も敵が途切れないよう、指定した区間を繰り返す
        [Label("繰り返す")]
        [SerializeField] private bool mIsLoop = false;
        // 段階の番号は 1 から数える
        [Label("繰り返しの始まり(段階の番号)")]
        [EditCondition(nameof(mIsLoop), true, false)]
        [Min(1)]
        [SerializeField] private int mLoopStart = 1;
        // 0 なら最後の段階まで
        [Label("繰り返しの終わり(段階の番号。0 で最後)")]
        [EditCondition(nameof(mIsLoop), true, false)]
        [Min(0)]
        [SerializeField] private int mLoopEnd = 0;

        /* プロパティ */

        public IReadOnlyList<PPWaveStep> Steps => mSteps;
        public bool IsLoop => mIsLoop && mSteps.Count > 0;
        // 繰り返し区間の始まりと終わり。0 から数えた添字で、段階の範囲に収める
        public int LoopStartIndex => Mathf.Clamp(mLoopStart - 1, 0, Mathf.Max(0, mSteps.Count - 1));
        public int LoopEndIndex => mLoopEnd <= 0 ? mSteps.Count - 1 : Mathf.Clamp(mLoopEnd - 1, LoopStartIndex, Mathf.Max(0, mSteps.Count - 1));

        /* 生成 */

        public PPWaveDefinition() { }

        public PPWaveDefinition(IEnumerable<PPWaveStep> aSteps, bool aIsLoop, int aLoopStart, int aLoopEnd)
        {
            mSteps = new List<PPWaveStep>(aSteps);
            mIsLoop = aIsLoop;
            mLoopStart = aLoopStart;
            mLoopEnd = aLoopEnd;
        }

        /* 問い合わせ */

        // 出てくるユニットの種類。同じユニットは 1 度だけ並べる
        public List<PPUnitDefinition> CollectUnits()
        {
            var units = new List<PPUnitDefinition>();
            foreach (var step in mSteps)
            {
                if (step == null) continue;
                foreach (var spawn in step.Spawns)
                {
                    if (spawn?.Unit != null && !units.Contains(spawn.Unit)) units.Add(spawn.Unit);
                }
            }
            return units;
        }

        // その種類のユニットを出す設定のうち、最も高いレベル。出さなければ 0
        public int GetMaxLevel(PPUnitDefinition aUnit)
        {
            int level = 0;
            foreach (var step in mSteps)
            {
                if (step == null) continue;
                foreach (var spawn in step.Spawns)
                {
                    if (spawn?.Unit == aUnit) level = Mathf.Max(level, spawn.Level);
                }
            }
            return level;
        }
    }

    // 段階が始まる条件を判定するときに渡す、今のバトルの様子。段階が待ち始めた時点の値も持つ
    public class PPWaveTriggerContext
    {
        /* 状態 */

        private readonly Func<int, bool> mIsStepWiped;
        private readonly Func<BattleSide, int> mGetBaseDamageCount;
        private readonly Func<BattleSide, float> mGetBaseHpRatio;
        private readonly Func<PPWaveTrigger, bool> mHasFired;

        /* プロパティ */

        // バトル専用の時計の今の時刻
        public float Now { get; set; }
        // この段階が待ち始めた時刻(前の段階が始まった時刻)
        public float ArmedTime { get; set; }
        // 待ち始めた時点で、各拠点がダメージを受けていた回数
        public int AllyDamageCountAtArm { get; set; }
        public int EnemyDamageCountAtArm { get; set; }

        /* 生成 */

        public PPWaveTriggerContext(Func<int, bool> aIsStepWiped, Func<BattleSide, int> aGetBaseDamageCount, Func<BattleSide, float> aGetBaseHpRatio,
            Func<PPWaveTrigger, bool> aHasFired)
        {
            mIsStepWiped = aIsStepWiped;
            mGetBaseDamageCount = aGetBaseDamageCount;
            mGetBaseHpRatio = aGetBaseHpRatio;
            mHasFired = aHasFired;
        }

        /* 問い合わせ */

        // 0 から数えた段階が、出し終えたうえで出した敵がすべて倒されているか
        public bool IsStepWiped(int aStepIndex) => mIsStepWiped(aStepIndex);

        // 待ち始めてから、その陣営の拠点がダメージを受けた回数
        public int GetBaseDamageCountSinceArmed(BattleSide aSide)
            => mGetBaseDamageCount(aSide) - (aSide == BattleSide.Ally ? AllyDamageCountAtArm : EnemyDamageCountAtArm);

        // その陣営の拠点の HP の割合(0〜1)。拠点が無ければ 1
        public float GetBaseHpRatio(BattleSide aSide) => mGetBaseHpRatio(aSide);

        // その条件で、このバトル中にすでに段階を始めたことがあるか
        public bool HasFired(PPWaveTrigger aTrigger) => mHasFired(aTrigger);
    }

    // 段階が始まる条件の基底。[SerializeReference] で段階に並べ、インスペクタで種類を選ぶ
    // 定義アセットの中で共有されるため、実行中の状態は持たない(判定に要るものは PPWaveTriggerContext から読む)
    [Serializable]
    public abstract class PPWaveTrigger
    {
        /* 評価・判定 */

        public abstract bool IsSatisfied(PPWaveTriggerContext aContext);
    }

    // 前の段階が始まってから、指定した秒数が経ったら
    [Serializable]
    [PPTypeMenuName("時間が経ったら")]
    public class PPWaveElapsedTrigger : PPWaveTrigger
    {
        /* インスペクタ設定 */

        // バトル専用の時計で数えるため、メニューを開いている間は進まない
        [Label("前の段階が始まってからの秒数")]
        [Min(0f)]
        [SerializeField] private float mSeconds = 10f;

        /* 生成 */

        public PPWaveElapsedTrigger() { }

        public PPWaveElapsedTrigger(float aSeconds) => mSeconds = aSeconds;

        /* 評価・判定 */

        public override bool IsSatisfied(PPWaveTriggerContext aContext) => aContext.Now - aContext.ArmedTime >= mSeconds;
    }

    // 指定した段階の敵を出し終え、すべて倒したら。無限に出す敵を含む段階では満たされない
    [Serializable]
    [PPTypeMenuName("段階の敵が全滅したら")]
    public class PPWaveStepWipedTrigger : PPWaveTrigger
    {
        /* インスペクタ設定 */

        [Label("段階の番号")]
        [Min(1)]
        [SerializeField] private int mStepNumber = 1;

        /* 生成 */

        public PPWaveStepWipedTrigger() { }

        public PPWaveStepWipedTrigger(int aStepNumber) => mStepNumber = aStepNumber;

        /* 評価・判定 */

        public override bool IsSatisfied(PPWaveTriggerContext aContext) => aContext.IsStepWiped(mStepNumber - 1);
    }

    // 待ち始めてから、指定した陣営の拠点がダメージを受けたら
    // 「1 度だけ」にすると、この条件で段階を始めるのはバトル中に 1 回だけになる(繰り返しで戻ってきても、もう満たさない)
    [Serializable]
    [PPTypeMenuName("拠点がダメージを受けたら")]
    public class PPWaveBaseDamagedTrigger : PPWaveTrigger
    {
        /* インスペクタ設定 */

        [Label("拠点")]
        [SerializeField] private BattleSide mSide = BattleSide.Enemy;
        [Label("1 度だけ")]
        [SerializeField] private bool mIsOnce = false;

        /* 生成 */

        public PPWaveBaseDamagedTrigger() { }

        public PPWaveBaseDamagedTrigger(BattleSide aSide, bool aIsOnce = false)
        {
            mSide = aSide;
            mIsOnce = aIsOnce;
        }

        /* 評価・判定 */

        public override bool IsSatisfied(PPWaveTriggerContext aContext)
        {
            if (mIsOnce && aContext.HasFired(this)) return false;
            return aContext.GetBaseDamageCountSinceArmed(mSide) > 0;
        }
    }

    // 指定した陣営の拠点の HP が、指定した割合以下になったら
    [Serializable]
    [PPTypeMenuName("拠点の HP が x% 以下になったら")]
    public class PPWaveBaseHpTrigger : PPWaveTrigger
    {
        /* インスペクタ設定 */

        [Label("拠点")]
        [SerializeField] private BattleSide mSide = BattleSide.Enemy;
        [Label("HP の割合(%)")]
        [Min(0f)]
        [SerializeField] private float mPercent = 50f;

        /* 生成 */

        public PPWaveBaseHpTrigger() { }

        public PPWaveBaseHpTrigger(BattleSide aSide, float aPercent)
        {
            mSide = aSide;
            mPercent = aPercent;
        }

        /* 評価・判定 */

        public override bool IsSatisfied(PPWaveTriggerContext aContext) => aContext.GetBaseHpRatio(mSide) <= Mathf.Clamp(mPercent, 0f, 100f) / 100f;
    }
}
