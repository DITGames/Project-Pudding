/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveStage.cs
 * @author hqrse
 * @date 2026/11/26
 * @brief ウェーブの段階(出す敵の表・始まる条件・この段階のあとの行き先)と、その要約の文
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // 段階が始まる条件の種類
    public enum PPWaveStartKind
    {
        // 待ち始めてから指定の秒数が経ったら
        [InspectorName("時間が経ったら")]
        Elapsed,
        // 指定の段階の最新の回が、出し終えたうえで全滅したら
        [InspectorName("段階の敵が全滅したら")]
        StageWiped,
        // 待ち始めてから、指定の陣営の拠点がダメージを受けたら
        [InspectorName("拠点がダメージを受けたら")]
        BaseDamaged,
        [InspectorName("拠点の HP が x% 以下になったら")]
        BaseHp,
    }

    // 行き先へ進むとき
    public enum PPWaveNextTiming
    {
        [InspectorName("出し始めたら")]
        Started,
        [InspectorName("出し終えたら")]
        Finished,
        [InspectorName("全滅したら")]
        Wiped,
    }

    // 行き先へ進むかを、進むときにその場で判定する条件
    public enum PPWaveNextCondition
    {
        [InspectorName("いつでも")]
        None,
        [InspectorName("拠点の HP が x% 以下なら")]
        BaseHpAtMost,
        // 「以下なら」と組むと、境目で重ならない 2 つの道になる
        [InspectorName("拠点の HP が x% より上なら")]
        BaseHpAbove,
    }

    // 段階の中で 1 種類の敵を出す設定(表の 1 行)。段階が始まってから「最初の待ち」後に 1 体目を出し、以降は間隔ごとに出す
    [Serializable]
    public class PPWaveSpawn
    {
        /* インスペクタ設定 */

        [Label("ユニット")]
        [SerializeField] private PPUnitDefinition mUnit;
        // 0 なら、バトルが終わるまで出し続ける
        [Label("数(0 で無限)")]
        [Min(0)]
        [SerializeField] private int mCount = 5;
        [Label("間隔(秒)")]
        [Min(0.1f)]
        [SerializeField] private float mInterval = 3f;
        [Label("最初の待ち(秒)")]
        [Min(0f)]
        [SerializeField] private float mFirstDelay = 0f;

        /* プロパティ */

        public PPUnitDefinition Unit => mUnit;
        public int Count => Mathf.Max(0, mCount);
        public bool IsInfinite => mCount <= 0;
        public float Interval => Mathf.Max(0.1f, mInterval);
        public float FirstDelay => Mathf.Max(0f, mFirstDelay);
        // 最後の 1 体を出すまでの秒数(段階が始まってから)。無限なら最初の 1 体まで
        public float Duration => IsInfinite ? FirstDelay : FirstDelay + Interval * Mathf.Max(0, Count - 1);

        /* 生成 */

        public PPWaveSpawn() { }

        public PPWaveSpawn(PPUnitDefinition aUnit, int aCount, float aInterval, float aFirstDelay = 0f)
        {
            mUnit = aUnit;
            mCount = aCount;
            mInterval = aInterval;
            mFirstDelay = aFirstDelay;
        }
    }

    // 段階が始まる条件 1 つ。種類ごとに使う欄が違う(使わない欄は無視する)
    [Serializable]
    public class PPWaveStartCondition
    {
        /* インスペクタ設定 */

        [Label("種類")]
        [SerializeField] private PPWaveStartKind mKind = PPWaveStartKind.Elapsed;
        // 時間: バトル専用の時計で数えるため、メニューを開いている間は進まない
        [Label("待ち始めてからの秒数")]
        [Min(0f)]
        [SerializeField] private float mSeconds = 20f;
        // 全滅: 対象の段階の識別子
        [Label("対象の段階")]
        [SerializeField] private string mStageId;
        [Label("拠点")]
        [SerializeField] private BattleSide mSide = BattleSide.Enemy;
        [Label("HP の割合(%)")]
        [Min(0f)]
        [SerializeField] private float mPercent = 50f;
        // 入れると、この条件で 1 度始まったら、以降は待ち直してもこの条件では始まらない
        [Label("1 度だけ")]
        [SerializeField] private bool mIsOnce = false;

        /* プロパティ */

        public PPWaveStartKind Kind => mKind;
        public float Seconds => Mathf.Max(0f, mSeconds);
        public string StageId => mStageId;
        public BattleSide Side => mSide;
        public float Percent => Mathf.Clamp(mPercent, 0f, 100f);
        public bool IsOnce => mIsOnce;

        /* static 関数 */

        public static PPWaveStartCondition Elapsed(float aSeconds, bool aIsOnce = false) => new() { mKind = PPWaveStartKind.Elapsed, mSeconds = aSeconds, mIsOnce = aIsOnce };

        public static PPWaveStartCondition StageWiped(string aStageId, bool aIsOnce = false) => new() { mKind = PPWaveStartKind.StageWiped, mStageId = aStageId, mIsOnce = aIsOnce };

        public static PPWaveStartCondition BaseDamaged(BattleSide aSide, bool aIsOnce = false) => new() { mKind = PPWaveStartKind.BaseDamaged, mSide = aSide, mIsOnce = aIsOnce };

        public static PPWaveStartCondition BaseHp(BattleSide aSide, float aPercent, bool aIsOnce = false) => new() { mKind = PPWaveStartKind.BaseHp, mSide = aSide, mPercent = aPercent, mIsOnce = aIsOnce };

        // 拠点の呼び名(「敵拠点」など)
        public static string GetBaseName(BattleSide aSide) => aSide == BattleSide.Ally ? "味方拠点" : "敵拠点";

        // 秒数・割合を文に入れる形(小数は 2 桁まで、末尾の 0 は省く)
        public static string FormatNumber(float aValue) => aValue.ToString("0.##");

        /* 説明文 */

        // 条件の文。aIsShort なら段階の一覧に並べる短い形。時間の起点は、段階がいつ待ち始めるかで言い分ける
        public string Describe(PPWaveDefinition aWave, PPWaveStage aOwner, bool aIsShort)
        {
            string text = mKind switch
            {
                PPWaveStartKind.Elapsed => aIsShort ? $"{FormatNumber(Seconds)} 秒" : $"{aWave.GetWaitOriginText(aOwner)}から {FormatNumber(Seconds)} 秒",
                PPWaveStartKind.StageWiped => aIsShort ? $"{aWave.GetStageName(mStageId)}が全滅" : $"「{aWave.GetStageLabel(mStageId)}」が全滅",
                PPWaveStartKind.BaseDamaged => $"{GetBaseName(mSide)}が被弾",
                PPWaveStartKind.BaseHp => $"{GetBaseName(mSide)}の HP {FormatNumber(Percent)}% 以下",
                _ => mKind.ToString(),
            };
            return mIsOnce ? $"{text}(1 度だけ)" : text;
        }

    }

    // 段階のあとに待ち始めさせる段階(行き先)1 つ。進むときに条件があれば、その場で判定して成り立つときだけ進む
    [Serializable]
    public class PPWaveNext
    {
        /* インスペクタ設定 */

        [Label("行き先の段階")]
        [SerializeField] private string mTargetId;
        [Label("いつ")]
        [SerializeField] private PPWaveNextTiming mTiming = PPWaveNextTiming.Started;
        [Label("条件")]
        [SerializeField] private PPWaveNextCondition mCondition = PPWaveNextCondition.None;
        [Label("拠点")]
        [SerializeField] private BattleSide mSide = BattleSide.Enemy;
        [Label("HP の割合(%)")]
        [Min(0f)]
        [SerializeField] private float mPercent = 50f;

        /* プロパティ */

        public string TargetId => mTargetId;
        public PPWaveNextTiming Timing => mTiming;
        public PPWaveNextCondition Condition => mCondition;
        public BattleSide Side => mSide;
        public float Percent => Mathf.Clamp(mPercent, 0f, 100f);

        /* 生成 */

        public PPWaveNext() { }

        public PPWaveNext(string aTargetId, PPWaveNextTiming aTiming = PPWaveNextTiming.Started, PPWaveNextCondition aCondition = PPWaveNextCondition.None, BattleSide aSide = BattleSide.Enemy, float aPercent = 50f)
        {
            mTargetId = aTargetId;
            mTiming = aTiming;
            mCondition = aCondition;
            mSide = aSide;
            mPercent = aPercent;
        }

        /* 評価・判定 */

        // 進むときの条件が成り立つか。aHpRatio は条件の拠点の今の HP の割合(0〜1)
        public bool IsConditionMet(float aHpRatio)
        {
            const float compareEpsilon = 0.0001f;
            float threshold = Percent / 100f;
            return mCondition switch
            {
                PPWaveNextCondition.BaseHpAtMost => aHpRatio <= threshold + compareEpsilon,
                PPWaveNextCondition.BaseHpAbove => aHpRatio > threshold + compareEpsilon,
                _ => true,
            };
        }

        /* 説明文 */

        // 「出し終えたら 敵拠点の HP 50% 以下なら → 3 援軍 へ」の形。出し始めたら・いつでもは省く
        public string Describe(PPWaveDefinition aWave)
        {
            string timing = mTiming switch
            {
                PPWaveNextTiming.Finished => "出し終えたら ",
                PPWaveNextTiming.Wiped => "全滅したら ",
                _ => string.Empty,
            };
            string condition = mCondition switch
            {
                PPWaveNextCondition.BaseHpAtMost => $"{PPWaveStartCondition.GetBaseName(mSide)}の HP {PPWaveStartCondition.FormatNumber(Percent)}% 以下なら ",
                PPWaveNextCondition.BaseHpAbove => $"{PPWaveStartCondition.GetBaseName(mSide)}の HP {PPWaveStartCondition.FormatNumber(Percent)}% より上なら ",
                _ => string.Empty,
            };
            return $"{timing}{condition}→ {aWave.GetStageLabel(mTargetId)} へ";
        }
    }

    // ウェーブの段階 1 つ。待ち始めたら、始まる条件のどれか 1 つを満たしたときに 1 回分の敵を出し、行き先の段階を待ち始めさせる
    // 条件が無ければ、待ち始めたらすぐ始まる。定義アセットで共有されるため、実行中の状態は持たない(ランナーが持つ)
    [Serializable]
    public class PPWaveStage
    {
        /* インスペクタ設定 */

        // 行き先・全滅の条件から指す。画面には出さない
        [HideInInspector]
        [SerializeField] private string mId = NewId();
        [Label("名前")]
        [SerializeField] private string mName;
        // 入れると、バトル開始から待ち始める。入れなければ、他の段階の行き先にされたときから待つ
        [Label("バトル開始から待つ")]
        [SerializeField] private bool mWaitsFromBattleStart;
        [Label("出す敵", true)]
        [SerializeField] private List<PPWaveSpawn> mSpawns = new();
        [Label("始まる条件(どれか 1 つ)", true)]
        [SerializeField] private List<PPWaveStartCondition> mStartConditions = new();
        [Label("この段階のあと", true)]
        [SerializeField] private List<PPWaveNext> mNexts = new();

        /* プロパティ */

        public string Id => mId;
        public string Name => mName;
        public bool WaitsFromBattleStart => mWaitsFromBattleStart;
        public IReadOnlyList<PPWaveSpawn> Spawns => mSpawns;
        public IReadOnlyList<PPWaveStartCondition> StartConditions => mStartConditions;
        public IReadOnlyList<PPWaveNext> Nexts => mNexts;
        // ユニットを選んだ行に、数が 0(無限)の行があるか。あれば、出し終えることも全滅することもない
        public bool IsInfinite
        {
            get
            {
                foreach (var spawn in mSpawns)
                {
                    if (spawn?.Unit != null && spawn.IsInfinite) return true;
                }
                return false;
            }
        }
        // ユニットを選んだ行があるか
        public bool HasSpawnUnit
        {
            get
            {
                foreach (var spawn in mSpawns)
                {
                    if (spawn?.Unit != null) return true;
                }
                return false;
            }
        }
        // 1 回で出す敵の数(無限の行は数えない)
        public int FiniteEnemyCount
        {
            get
            {
                int count = 0;
                foreach (var spawn in mSpawns)
                {
                    if (spawn?.Unit != null && !spawn.IsInfinite) count += spawn.Count;
                }
                return count;
            }
        }
        // 始まってから出し終えるまでの秒数(無限の行は最初の 1 体まで)
        public float SpawnDuration
        {
            get
            {
                float duration = 0f;
                foreach (var spawn in mSpawns)
                {
                    if (spawn?.Unit != null) duration = Mathf.Max(duration, spawn.Duration);
                }
                return duration;
            }
        }

        /* static 関数 */

        public static string NewId() => Guid.NewGuid().ToString("N");

        /* 生成 */

        public PPWaveStage() { }

        public PPWaveStage(string aName, bool aWaitsFromBattleStart, IEnumerable<PPWaveSpawn> aSpawns, IEnumerable<PPWaveStartCondition> aStartConditions = null, IEnumerable<PPWaveNext> aNexts = null, string aId = null)
        {
            if (!string.IsNullOrEmpty(aId)) mId = aId;
            mName = aName;
            mWaitsFromBattleStart = aWaitsFromBattleStart;
            mSpawns = new List<PPWaveSpawn>(aSpawns ?? Array.Empty<PPWaveSpawn>());
            mStartConditions = new List<PPWaveStartCondition>(aStartConditions ?? Array.Empty<PPWaveStartCondition>());
            mNexts = new List<PPWaveNext>(aNexts ?? Array.Empty<PPWaveNext>());
        }

    }
}
