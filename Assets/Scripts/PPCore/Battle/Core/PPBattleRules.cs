/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleRules.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief バトルルール（判定の差し替え口・調整値・乱数供給）
 * =====================================*/

using System;
using System.Collections.Generic;

namespace PPCore
{
    // バトル中の判定ロジックと調整値を、差し替え可能なインターフェースでまとめた設定オブジェクト
    // 進行そのもの（キュー・ステート・演出やログの出力先）は BattleManager が持つ
    public class BattleRules
    {
        // 命中判定を行うリゾルバ
        public IHitResolver HitResolver { get; set; } = new StandardHitResolver();
        // クリティカル判定を行うリゾルバ
        public ICriticalResolver CriticalResolver { get; set; } = new StandardCriticalResolver();
        // バトル中の乱数供給元。AI を含め乱数は必ずここを経由させる
        public IRandomProvider RandomProvider { get; set; } = new DefaultRandomProvider();
        // スキル発動可否（コスト・クールダウン等）を検証するバリデータ
        public ICastValidator CastValidator { get; set; } = new DefaultCastValidator();
        // ターゲット候補に対して順に適用される絞り込みフィルタ群
        public List<ITargetFilter> TargetFilters { get; } = new();
        // 対象が死亡していた場合の代替ターゲット決定ポリシー
        public IDeadTargetPolicy DeadTargetPolicy { get; set; } = new FirstAliveFallback();
        // ターンごとの行動順並び替えクラス。既定は素早さ順
        public ITurnOrderResolver TurnOrderResolver { get; set; } = new SpeedTurnOrderResolver();
        // 勝敗判定クラス。差し替えることで引き分け条件などを追加できる
        public IBattleResultChecker ResultChecker { get; set; } = new DefaultBattleResultChecker();
        // 1 イベント当たりのリアクション上限
        // 想定外の連鎖に対する安全網であり、意図した連鎖の制御はリアクション側の条件判定で行う
        public int MaxReactionPerEvent { get; set; } = 1;
        // ユニットが撃破されたとき、掛かっているステータスエフェクトをすべて解除するか
        // 解除は撃破通知（BattleUnit.OnDefeated）の直前に行われる
        public bool ClearStatusEffectsOnDefeat { get; set; } = false;
    }

    // 属性相性のダメージ倍率や AI の危機判定閾値などの調整値を追加したバトルルール
    // BattleContext.Rules へこの型を差し込むことで有効になる
    public class PPBattleRules : BattleRules
    {
        /* 状態 */

        // AI の低HP判定閾値。HP 割合がこれを下回ると危機的状況として扱う
        public float CrisisHpRatio = 0.25f;
        // 弱点属性でヒットしたときのダメージ倍率
        public float WeaknessMultiplier = 1.5f;
        // 耐性属性でヒットしたときのダメージ倍率
        public float ResistanceMultiplier = 0.75f;
        // 通常攻撃（スキル未使用）の基礎AIスコア。スキルは PPSkillDefinition.AIScore 側に個別で持つのに対し、
        // 通常攻撃はスキル定義を持たないためここで一括して持つ
        public float NormalAttackAIScore = 1f;
        // 同時にコインゲージが溜まった行動を実行順に並べるリゾルバ
        // 優先度と、素早さに 0〜1 の乱数を足した値で並べるのが既定。並べ方を変えたい場合はここを差し替える
        public IPPActionOrderResolver ActionOrderResolver { get; set; } =
            new PPDefaultActionOrderResolver { SpeedJitter = 1f };
        // 通常攻撃 1 回あたりに回復するスキルゲージ量
        // 通常攻撃を撃ち続けることでスキルへ繋がる導線になるため、ユニットごとではなくルール側で一括して持つ
        public float NormalAttackSkillGaugeRecover = 5f;

        /* 生成 */

        // 既定のリゾルバとルール値を差し込む
        // 会心率は攻撃側のきようさから求め、戦闘不能になったユニットの状態異常はすべて解除する
        public PPBattleRules()
        {
            CriticalResolver = new PPCriticalResolver();
            ClearStatusEffectsOnDefeat = true;
        }
    }

    // バトル中の乱数供給元。命中判定・AI の思考・代替ターゲット選択などの乱数は全てここを経由させる
    // 再現性を保つため UnityEngine.Random を直接呼ばないこと
    public interface IRandomProvider
    {
        // 0 以上 aMaxExclusive 未満の整数を返す
        // aMaxExclusive : 上限（この値自体は含まない）
        int NextInt(int aMaxExclusive);

        // 指定範囲の整数を返す
        int NextInt(int aMinInclusive, int aMaxExclusive);

        // 0 以上 1 未満の実数を返す
        float NextFloat();

        // 指定範囲の実数を返す
        float NextFloat(float aMinInclusive, float aMaxExclusive);

        // 指定確率で true を返す
        // aTrueChance : true になる確率（0～1）
        bool NextBool(float aTrueChance);
    }

    // Random を用いた標準の乱数供給実装。シードを与えれば同じ乱数列を再現できる
    public class DefaultRandomProvider : IRandomProvider
    {
        /* 状態 */

        // 乱数生成器の実体
        protected readonly Random mRng;

        /* 生成 */

        // aSeed : 固定シード。null なら時刻由来のシードで初期化する
        public DefaultRandomProvider(int? aSeed = null)
            => mRng = aSeed.HasValue ? new Random(aSeed.Value) : new Random();

        /* 操作 */

        // 0 以上 aMaxExclusive 未満の整数を返す
        public int NextInt(int aMaxExclusive) => mRng.Next(aMaxExclusive);

        // 指定範囲の整数を返す
        public int NextInt(int aMinInclusive, int aMaxExclusive)  => mRng.Next(aMinInclusive, aMaxExclusive);

        // 0 以上 1 未満の実数を返す
        public float NextFloat() => (float)mRng.NextDouble();

        // 指定範囲の実数を返す
        public float NextFloat(float aMinInclusive, float aMaxExclusive) => aMinInclusive + (float)mRng.NextDouble() * (aMaxExclusive - aMinInclusive);

        // 指定確率で true を返す
        public bool NextBool(float aTrueChance) => mRng.NextDouble() < aTrueChance;
    }
}
