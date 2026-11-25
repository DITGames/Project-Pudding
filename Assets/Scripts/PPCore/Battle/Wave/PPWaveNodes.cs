/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveNodes.cs
 * @author hqrse
 * @date 2026/11/25
 * @brief ウェーブのノード(トリガー・ウェーブ・条件)と、ノードの判定に渡す今のバトルの様子
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Reflection;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ノードの判定に渡す、今のバトルの様子。ランナーが実装する
    public interface IPPWaveGraphContext
    {
        // バトル専用の時計の今の時刻
        float Now { get; }

        // バトルが始まってから、その陣営の拠点がダメージを受けた回数
        int GetBaseDamageCount(BattleSide aSide);

        // その陣営の拠点の HP の割合(0〜1)。拠点が無ければ 1
        float GetBaseHpRatio(BattleSide aSide);

        // 識別子のウェーブノードの最新の回が全滅しているか。aRunNumber はその回の番号(1 から数える。まだ始めていなければ 0)
        bool IsLatestRunWiped(string aNodeId, out int aRunNumber);
    }

    // トリガー 1 つ分の、実行中の待ちの状態。定義はアセットで共有されるため、ランナーが持つ
    public class PPWaveTriggerState
    {
        /* 状態 */

        public bool IsWaiting;
        // 待ち始めた時刻と、待ち始めた時点で各拠点がダメージを受けていた回数
        public float ArmedTime;
        public int AllyDamageCountAtArm;
        public int EnemyDamageCountAtArm;
        public int FireCount;
        // 起動したその場で待ち直したか。同じ出来事でもう一度起動しないよう、種類ごとに使う
        public bool IsRearmed;
        // 全滅のトリガーが最後に起動したときの、対象の回の番号
        public int LastFiredRunNumber;

        /* 問い合わせ */

        // 待ち始めてから、その陣営の拠点がダメージを受けた回数
        public int GetDamageCountSinceArmed(IPPWaveGraphContext aContext, BattleSide aSide)
            => aContext.GetBaseDamageCount(aSide) - (aSide == BattleSide.Ally ? AllyDamageCountAtArm : EnemyDamageCountAtArm);
    }

    // ノードの識別子を持つ文字列に付け、インスペクタで同じウェーブのノードを選べるようにする
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class PPWaveNodeReferenceAttribute : PropertyAttribute
    {
        /* プロパティ */

        public string Text { get; }
        public bool IsSpawnNodeOnly { get; }

        /* 生成 */

        public PPWaveNodeReferenceAttribute(string aText, bool aIsSpawnNodeOnly = false)
        {
            Text = aText;
            IsSpawnNodeOnly = aIsSpawnNodeOnly;
        }
    }

    // ノードの識別子の文字列に付け、インスペクタで書き換えられない欄にする。書き換えると、その識別子を指す線がすべて切れるため
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class PPWaveNodeIdAttribute : PropertyAttribute
    {
        /* プロパティ */

        public string Text { get; }

        /* 生成 */

        public PPWaveNodeIdAttribute(string aText) => Text = aText;
    }

    // ウェーブのノードの基底。[SerializeReference] で並べ、線は識別子で指す
    // 定義アセットの中で共有されるため、実行中の状態は持たない(ランナーが持つ)
    [Serializable]
    public abstract class PPWaveNode
    {
        /* インスペクタ設定 */

        [PPWaveNodeId("識別子")]
        [SerializeField] private string mId = NewId();
        // インスペクタで見分けるための名前。動きには使わない
        [Label("メモ")]
        [SerializeField] private string mMemo;
        // ノードエディタで置く位置。動きには使わない
        [HideInInspector]
        [SerializeField] private Vector2 mPosition;

        /* プロパティ */

        public string Id => mId;
        public string Memo => mMemo;
        public Vector2 Position { get => mPosition; set => mPosition = value; }
        // このノードが持つ出口
        public abstract IReadOnlyList<PPWaveNodePort> Outputs { get; }
        // 種類の名前([PPTypeMenuName] の最後の区切り)
        public string TypeName
        {
            get
            {
                string path = GetType().GetCustomAttribute<PPTypeMenuNameAttribute>()?.Path ?? GetType().Name;
                int slash = path.LastIndexOf('/');
                return slash >= 0 ? path.Substring(slash + 1) : path;
            }
        }
        // ログやインスペクタに出す名前。メモが無ければ種類の名前
        public string DisplayName => string.IsNullOrEmpty(mMemo) ? TypeName : mMemo;

        /* 生成 */

        protected PPWaveNode() { }

        protected PPWaveNode(string aMemo) => mMemo = aMemo;

        /* 操作 */

        // 識別子を新しく振り直す。インスペクタで複製して識別子が重なったときに使う
        public void RegenerateId() => mId = NewId();

        /* 問い合わせ */

        public bool HasOutput(PPWaveNodePort aPort)
        {
            foreach (var port in Outputs)
            {
                if (port == aPort) return true;
            }
            return false;
        }

        /* ヘルパー */

        private static string NewId() => Guid.NewGuid().ToString("N");
    }

    // トリガーノードの基底。待っている間に条件を満たしたら起動し、「起動したとき」の先へ進む
    // 入口に線が無ければバトル開始から待ち、線があれば入口から動かされたときから待つ
    [Serializable]
    public abstract class PPWaveTriggerNode : PPWaveNode
    {
        /* 定数 */

        private static readonly PPWaveNodePort[] TriggerOutputs = { PPWaveNodePort.Fired };

        /* プロパティ */

        public override IReadOnlyList<PPWaveNodePort> Outputs => TriggerOutputs;
        // バトル中に 1 回だけ起動するか
        public virtual bool IsOnce => true;
        // 早い者勝ちのグループ。同じグループのどれかが起動したら、他は待つのをやめる。null か空なら無し
        public virtual string RaceGroup => null;
        // 入口からの線を受け付けず、いつもバトル開始から待つか
        public virtual bool IgnoresInput => false;

        /* 生成 */

        protected PPWaveTriggerNode() { }

        protected PPWaveTriggerNode(string aMemo) : base(aMemo) { }

        /* 評価・判定 */

        // 待っている間に、起動する条件を満たしたか。状態は読むだけで書き換えない
        public abstract bool IsSatisfied(PPWaveTriggerState aState, IPPWaveGraphContext aContext);

        // 待っている間、判定の前にランナーが毎フレーム呼ぶ。待ち直しの見届けなど、判定に使う状態の更新はここで行う
        public virtual void Observe(PPWaveTriggerState aState, IPPWaveGraphContext aContext) { }

        // 起動したときに、次の判定に要る値を覚える
        public virtual void OnFired(PPWaveTriggerState aState, IPPWaveGraphContext aContext) { }
    }

    // バトルが始まったら 1 度だけ起動する。入口からの線は受け付けない
    [Serializable]
    [PPTypeMenuName("トリガー/バトル開始")]
    public class PPWaveBattleStartTriggerNode : PPWaveTriggerNode
    {
        /* プロパティ */

        public override bool IgnoresInput => true;

        /* 生成 */

        public PPWaveBattleStartTriggerNode() { }

        public PPWaveBattleStartTriggerNode(string aMemo) : base(aMemo) { }

        /* 評価・判定 */

        public override bool IsSatisfied(PPWaveTriggerState aState, IPPWaveGraphContext aContext) => true;
    }

    // 「1 度だけ」と「早い者勝ちのグループ」を持つトリガーの基底。何度でもなら、入口に線が無いときは起動したその場で待ち直す
    [Serializable]
    public abstract class PPWaveWaitTriggerNode : PPWaveTriggerNode
    {
        /* インスペクタ設定 */

        [Label("1 度だけ")]
        [SerializeField] private bool mIsOnce = false;
        // 同じ名前を付けたトリガーのどれかが起動したら、他は次に動かされるまで待つのをやめる。空なら無し
        [Label("早い者勝ちのグループ")]
        [SerializeField] private string mRaceGroup;

        /* プロパティ */

        public override bool IsOnce => mIsOnce;
        public override string RaceGroup => mRaceGroup;

        /* 生成 */

        protected PPWaveWaitTriggerNode() { }

        protected PPWaveWaitTriggerNode(string aMemo, bool aIsOnce, string aRaceGroup) : base(aMemo)
        {
            mIsOnce = aIsOnce;
            mRaceGroup = aRaceGroup;
        }
    }

    // 待ち始めてから、指定した秒数が経ったら
    [Serializable]
    [PPTypeMenuName("トリガー/時間が経ったら")]
    public class PPWaveElapsedTriggerNode : PPWaveWaitTriggerNode
    {
        /* 定数 */

        private const float CompareEpsilon = 0.0001f;

        /* インスペクタ設定 */

        // バトル専用の時計で数えるため、メニューを開いている間は進まない
        [Label("待ち始めてからの秒数")]
        [Min(0f)]
        [SerializeField] private float mSeconds = 10f;

        /* プロパティ */

        public float Seconds => Mathf.Max(0f, mSeconds);

        /* 生成 */

        public PPWaveElapsedTriggerNode() { }

        public PPWaveElapsedTriggerNode(string aMemo, float aSeconds, bool aIsOnce = false, string aRaceGroup = null) : base(aMemo, aIsOnce, aRaceGroup)
            => mSeconds = aSeconds;

        /* 評価・判定 */

        public override bool IsSatisfied(PPWaveTriggerState aState, IPPWaveGraphContext aContext)
            => aContext.Now - aState.ArmedTime >= Seconds - CompareEpsilon;
    }

    // 待ち始めてから、指定した陣営の拠点がダメージを受けたら
    [Serializable]
    [PPTypeMenuName("トリガー/拠点がダメージを受けたら")]
    public class PPWaveBaseDamagedTriggerNode : PPWaveWaitTriggerNode
    {
        /* インスペクタ設定 */

        [Label("拠点")]
        [SerializeField] private BattleSide mSide = BattleSide.Enemy;

        /* 生成 */

        public PPWaveBaseDamagedTriggerNode() { }

        public PPWaveBaseDamagedTriggerNode(string aMemo, BattleSide aSide, bool aIsOnce = false, string aRaceGroup = null) : base(aMemo, aIsOnce, aRaceGroup)
            => mSide = aSide;

        /* 評価・判定 */

        public override bool IsSatisfied(PPWaveTriggerState aState, IPPWaveGraphContext aContext) => aState.GetDamageCountSinceArmed(aContext, mSide) > 0;
    }

    // 指定した陣営の拠点の HP が、指定した割合以下なら。待ち直したときは、一度その割合より上に戻ってから下がったら
    [Serializable]
    [PPTypeMenuName("トリガー/拠点の HP が x% 以下になったら")]
    public class PPWaveBaseHpTriggerNode : PPWaveWaitTriggerNode
    {
        /* 定数 */

        private const float CompareEpsilon = 0.0001f;

        /* インスペクタ設定 */

        [Label("拠点")]
        [SerializeField] private BattleSide mSide = BattleSide.Enemy;
        [Label("HP の割合(%)")]
        [Min(0f)]
        [SerializeField] private float mPercent = 50f;

        /* 生成 */

        public PPWaveBaseHpTriggerNode() { }

        public PPWaveBaseHpTriggerNode(string aMemo, BattleSide aSide, float aPercent, bool aIsOnce = false, string aRaceGroup = null) : base(aMemo, aIsOnce, aRaceGroup)
        {
            mSide = aSide;
            mPercent = aPercent;
        }

        /* 評価・判定 */

        // 待ち直した後は、割合より上に戻ったのを見届けるまで(IsRearmed が下りるまで)起動しない
        public override bool IsSatisfied(PPWaveTriggerState aState, IPPWaveGraphContext aContext) => !aState.IsRearmed && IsBelow(aContext);

        // 待ち直した後、割合より上に戻ったら IsRearmed を下ろす
        public override void Observe(PPWaveTriggerState aState, IPPWaveGraphContext aContext)
        {
            if (aState.IsRearmed && !IsBelow(aContext)) aState.IsRearmed = false;
        }

        private bool IsBelow(IPPWaveGraphContext aContext) => aContext.GetBaseHpRatio(mSide) <= Mathf.Clamp(mPercent, 0f, 100f) / 100f + CompareEpsilon;
    }

    // 指定したウェーブノードの最新の回が、出し終えたうえで全滅したら。待ち直した後は、次の回が全滅したら
    [Serializable]
    [PPTypeMenuName("トリガー/ウェーブの敵が全滅したら")]
    public class PPWaveWipedTriggerNode : PPWaveWaitTriggerNode
    {
        /* インスペクタ設定 */

        [PPWaveNodeReference("対象のウェーブ", true)]
        [SerializeField] private string mTargetId;

        /* プロパティ */

        public string TargetId => mTargetId;

        /* 生成 */

        public PPWaveWipedTriggerNode() { }

        public PPWaveWipedTriggerNode(string aMemo, string aTargetId, bool aIsOnce = false, string aRaceGroup = null) : base(aMemo, aIsOnce, aRaceGroup)
            => mTargetId = aTargetId;

        /* 評価・判定 */

        public override bool IsSatisfied(PPWaveTriggerState aState, IPPWaveGraphContext aContext)
        {
            if (!aContext.IsLatestRunWiped(mTargetId, out int runNumber)) return false;
            return !aState.IsRearmed || runNumber != aState.LastFiredRunNumber;
        }

        public override void OnFired(PPWaveTriggerState aState, IPPWaveGraphContext aContext)
        {
            aContext.IsLatestRunWiped(mTargetId, out int runNumber);
            aState.LastFiredRunNumber = runNumber;
        }
    }

    // ウェーブノード。動かされるたびに新しい回を始め、出す敵を敵拠点の近くへ出す。出している最中に動かされても回を重ねる
    [Serializable]
    [PPTypeMenuName("ウェーブ")]
    public class PPWaveSpawnNode : PPWaveNode
    {
        /* 定数 */

        private static readonly PPWaveNodePort[] SpawnOutputs = { PPWaveNodePort.Started, PPWaveNodePort.Finished, PPWaveNodePort.Wiped };

        /* インスペクタ設定 */

        [Label("出す敵", true)]
        [SerializeField] private List<PPWaveSpawn> mSpawns = new();

        /* プロパティ */

        public override IReadOnlyList<PPWaveNodePort> Outputs => SpawnOutputs;
        public IReadOnlyList<PPWaveSpawn> Spawns => mSpawns;
        // 数が 0(無限)の敵を出すか。出すなら、出し終えることも全滅することもない
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

        /* 生成 */

        public PPWaveSpawnNode() { }

        public PPWaveSpawnNode(string aMemo, IEnumerable<PPWaveSpawn> aSpawns) : base(aMemo) => mSpawns = new List<PPWaveSpawn>(aSpawns);
    }

    // 条件ノードの基底。動かされたらその場で判定し、「成り立つ」か「成り立たない」の先へ進む
    [Serializable]
    public abstract class PPWaveConditionNode : PPWaveNode
    {
        /* 定数 */

        private static readonly PPWaveNodePort[] ConditionOutputs = { PPWaveNodePort.True, PPWaveNodePort.False };

        /* プロパティ */

        public override IReadOnlyList<PPWaveNodePort> Outputs => ConditionOutputs;

        /* 生成 */

        protected PPWaveConditionNode() { }

        protected PPWaveConditionNode(string aMemo) : base(aMemo) { }

        /* 評価・判定 */

        public abstract bool Evaluate(IPPWaveGraphContext aContext);
    }

    // 割合と比べる向き
    public enum PPWaveCompare
    {
        [InspectorName("以上")]
        AtLeast,
        [InspectorName("以下")]
        AtMost,
    }

    // 指定した陣営の拠点の HP の割合が、指定した割合以上(以下)か
    [Serializable]
    [PPTypeMenuName("条件/拠点の HP の割合")]
    public class PPWaveBaseHpConditionNode : PPWaveConditionNode
    {
        /* 定数 */

        private const float CompareEpsilon = 0.0001f;

        /* インスペクタ設定 */

        [Label("拠点")]
        [SerializeField] private BattleSide mSide = BattleSide.Enemy;
        [Label("比べ方")]
        [SerializeField] private PPWaveCompare mCompare = PPWaveCompare.AtLeast;
        [Label("HP の割合(%)")]
        [Min(0f)]
        [SerializeField] private float mPercent = 50f;

        /* 生成 */

        public PPWaveBaseHpConditionNode() { }

        public PPWaveBaseHpConditionNode(string aMemo, BattleSide aSide, PPWaveCompare aCompare, float aPercent) : base(aMemo)
        {
            mSide = aSide;
            mCompare = aCompare;
            mPercent = aPercent;
        }

        /* 評価・判定 */

        public override bool Evaluate(IPPWaveGraphContext aContext)
        {
            float ratio = aContext.GetBaseHpRatio(mSide);
            float threshold = Mathf.Clamp(mPercent, 0f, 100f) / 100f;
            return mCompare == PPWaveCompare.AtLeast ? ratio >= threshold - CompareEpsilon : ratio <= threshold + CompareEpsilon;
        }
    }
}
