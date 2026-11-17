/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStatusEffectDuration.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief ステータスエフェクトの持続条件（ランタイムと定義）
 * =====================================*/

using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;
using AttributeUtility;

namespace PPCore
{
    // ステータスエフェクトが「何秒おきに発生し、いつ切れるか」を表す持続条件
    // BattleUnit が Interval 秒ごとにタイマーで効果を発生させ、そのたびに Tick を呼ぶ。false が返った時点でエフェクトを除去する
    public interface IDurationCondition
    {
        // 効果が発生する間隔（秒）。バトル専用の時計で計る。0 以下なら発生せず、自然には切れない
        float Interval { get; }

        // 効果が 1 回発生するたびに呼ばれる。続けるなら true を返す
        bool Tick();
    }

    // 継続時間を延長し直せる持続条件であることを示すインターフェース
    // StatusEffectStackPolicy.Refresh 系の重ね掛けで使われる
    public interface IRefreshableDuration
    {
        // 継続時間を初期値へ戻す
        void Refresh();
    }

    // 「何秒おきに発生するか」と「何回発生するか」で決まる持続条件。間隔 × 回数の秒数が過ぎると切れる
    public sealed class IntervalDurationCondition : IDurationCondition, IRefreshableDuration
    {
        /* 状態 */

        // 効果が発生する間隔（秒）
        public float Interval { get; }
        // 残りの発生回数
        public int RemainingCount { get; private set; }
        // リフレッシュ時に戻す初期の発生回数
        private readonly int mInitialCount;

        /* 生成 */

        public IntervalDurationCondition(float aInterval, int aCount)
        {
            Interval = aInterval;
            mInitialCount = aCount;
            RemainingCount = aCount;
        }

        /* 操作 */

        // 残りの発生回数を 1 減らし、まだ残っていれば継続を返す
        public bool Tick() => --RemainingCount > 0;

        // 残りの発生回数を初期値へ戻す
        public void Refresh() => RemainingCount = mInitialCount;
    }

    // 自然には切れない持続条件。解除するにはアイテムや解除スキルで明示的に除去する必要がある
    // 毒のように切れないまま効果が発生し続けるものは、発生間隔だけを持つ
    public sealed class PermanentDurationCondition : IDurationCondition
    {
        // 間隔を指定しないときの発生間隔（秒）
        public const float DefaultInterval = 5f;

        // 効果が発生する間隔（秒）
        public float Interval { get; }

        public PermanentDurationCondition(float aInterval = DefaultInterval) => Interval = aInterval;

        // 常に継続を返す
        public bool Tick() => true;
    }

    // 任意の条件式で継続可否を制御する汎用実装
    // 「HP が一定以下の間だけ」といった、回数で表せない持続条件に使う。条件は発生間隔ごとに評価される
    public sealed class PredicateDurationCondition : IDurationCondition
    {
        // 継続すべきかを判定する関数
        private readonly System.Func<bool> mShouldContinueFunc;

        // 効果が発生する間隔（秒）
        public float Interval { get; }

        // aShouldContinueFunc : 継続する間 true を返す関数
        public PredicateDurationCondition(System.Func<bool> aShouldContinueFunc,
            float aInterval = PermanentDurationCondition.DefaultInterval)
        {
            mShouldContinueFunc = aShouldContinueFunc;
            Interval = aInterval;
        }

        // 条件式を評価してそのまま返す
        public bool Tick() => mShouldContinueFunc();
    }

    // StatusEffect の持続条件（IDurationCondition）をインスペクタ上で選ぶためのデータ定義
    // 種類はドロワー（PPDurationConditionDefinitionDrawer）のプルダウンから選ぶ
    [Serializable]
    public abstract class PPDurationConditionDefinition
    {
        // この定義から持続条件のランタイムインスタンスを生成する
        // 状態を持つため、付与のたびに必ず新しいインスタンスを返すこと
        public abstract IDurationCondition CreateDurationCondition();

        // フィールドラベルに表示する、この持続条件の内容を要約した文字列を組み立てる
        public abstract string BuildString();
    }

    // 時間経過では切れない永続の持続条件の定義。解除にはアイテムや解除スキルが要る
    // 解除スキルからも消せないようにするには、エフェクト側のタグに Unremovable を立てる
    [Serializable]
    [PPTypeMenuName("永続")]
    public class PPPermanentDurationConditionDefinition : PPDurationConditionDefinition
    {
        /* インスペクタ設定 */

        // 継続ダメージなどの効果が発生する間隔。付与時に適用するだけの効果（バフなど）では使われない
        [Label("発生間隔(秒)")][Min(0.1f)]
        [SerializeField]protected float mInterval = PermanentDurationCondition.DefaultInterval;

        /* 生成 */

        public override IDurationCondition CreateDurationCondition() => new PermanentDurationCondition(mInterval);

        /* 説明文 */

        public override string BuildString() => $"永続（{mInterval}秒ごと）";
    }

    // 「何秒おきに発生するか」と「何回発生するか」で決まる持続条件の定義。間隔 × 回数の秒数が過ぎると自然に切れる
    // 大半のバフ・デバフ・状態異常はこちらを使う
    [Serializable]
    [PPTypeMenuName("間隔 × 回数")]
    [MovedFrom(false, null, null, "PPTurnDurationConditionDefinition")]
    public class PPIntervalDurationConditionDefinition : PPDurationConditionDefinition
    {
        /* インスペクタ設定 */

        // 効果が発生する間隔。継続ダメージはこの間隔ごとに発生する
        [Label("発生間隔(秒)")][Min(0.1f)]
        [SerializeField]protected float mInterval = 5f;
        // 効果が発生する回数。この回数だけ発生すると切れる
        [Label("発生回数")][Min(1)]
        [FormerlySerializedAs("mDuration")]
        [SerializeField]protected int mCount = 3;

        /* プロパティ */

        public float Interval => mInterval;
        public int Count => mCount;
        // 付与から切れるまでの秒数
        public float TotalSeconds => mInterval * mCount;

        /* 生成 */

        public override IDurationCondition CreateDurationCondition() => new IntervalDurationCondition(mInterval, mCount);

        /* 説明文 */

        public override string BuildString() => $"{mInterval}秒 × {mCount}回（{TotalSeconds}秒）";
    }
}