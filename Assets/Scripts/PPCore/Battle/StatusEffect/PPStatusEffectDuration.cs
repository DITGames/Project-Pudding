/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStatusEffectDuration.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief ステータスエフェクトの持続条件（ランタイムと定義）
 * =====================================*/

using System;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // ステータスエフェクトが「いつ切れるか」を表す持続条件
    // BattleUnit.TickStatusEffects が毎更新で Tick を呼び、false が返った時点でエフェクトを除去する
    public interface IDurationCondition
    {
        // 更新のたびに呼ばれる
        // ターン制バトルならターン毎、ATB なら一定時間毎など
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
    // Tick は減算してから判定する。付与直後にも Tick が回る前提の呼び出し順になっている点に注意
    public sealed class TurnDurationCondition : IDurationCondition, IRefreshableDuration
    {
        /* 状態 */

        // 残りターン数
        public int RemainingDuration {get; private set;}
        // リフレッシュ時に戻す初期ターン数
        private readonly int mInitialDuration;

        /* 生成 */

        // aDuration : 持続ターン数
        public TurnDurationCondition(int aDuration)
        {
            mInitialDuration = aDuration;
            RemainingDuration = aDuration;
        }

        /* 操作 */

        // 残りターン数を 1 減らし、まだ残っていれば継続を返す
        public bool Tick() => --RemainingDuration > 0;

        /* 表示 */

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

    // ターン経過では切れない永続の持続条件の定義。解除にはアイテムや解除スキルが要る
    // 解除スキルからも消せないようにするには、エフェクト側のタグに Unremovable を立てる
    [Serializable]
    [PPTypeMenuName("永続")]
    public class PPPermanentDurationConditionDefinition : PPDurationConditionDefinition
    {
        public override IDurationCondition CreateDurationCondition() => new PermanentDurationCondition();

        public override string BuildString() => "永続";
    }

    // 指定ターン数が経過すると自然に切れる持続条件の定義
    // 大半のバフ・デバフ・状態異常はこちらを使う
    [Serializable]
    [PPTypeMenuName("ターン経過")]
    public class PPTurnDurationConditionDefinition : PPDurationConditionDefinition
    {
        /* インスペクタ設定 */

        // 持続条件のドロワーが種類のプルダウンと同じ行に並べるため、[Header] は付けない
        [Label("期間")][Min(1)]
        [SerializeField]protected int mDuration = 3;

        /* プロパティ */

        public int Duration => mDuration;

        /* 生成 */

        public override IDurationCondition CreateDurationCondition() => new TurnDurationCondition(mDuration);

        /* 説明文 */

        public override string BuildString() => $"{mDuration}ターン";
    }
}
