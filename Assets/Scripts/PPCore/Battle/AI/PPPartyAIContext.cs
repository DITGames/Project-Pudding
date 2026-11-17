/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPartyAIContext.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief パーティ状況のスナップショット
 * =====================================*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace PPCore
{
    // 思考開始時点のパーティ状況のスナップショット。Capture で 1 回だけ集計し、以降の評価は全てこれを参照する
    // 思考の途中で状況が変わらないことを保証する
    public sealed class PPPartyAIContext
    {
        /* 状態 */

        // 思考主体のパーティ
        public PPBattleParty Party { get; private set; }
        // 参照元のバトルコンテキスト。乱数やルールを引くのに使う
        public BattleContext Context { get; private set; }
        // スナップショット時点で生存しているアクティブな味方
        public List<PPBattleUnit> AliveMembers { get; } = new();
        // スナップショット時点で生存しているアクティブな敵
        public List<PPBattleUnit> AliveEnemies { get; } = new();
        // HP 実数値が最も低い敵。とどめを狙う際の第一候補になる
        public PPBattleUnit LowestHpEnemy { get; private set; }
        // HP 割合が最も低い敵。AI の対象選択から引かれる
        public PPBattleUnit LowestHpRatioEnemy { get; private set; }
        // HP 割合が最も低い味方。回復スキルの対象になる
        public PPBattleUnit LowestHpRatioAlly { get; private set; }
        // HP 実数値が最も高い敵
        public PPBattleUnit HighestHpEnemy { get; private set; }
        // HP 割合が最も高い味方
        public PPBattleUnit HighestHpRatioAlly { get; private set; }
        // 味方内で最も低い HP 割合。0～1 で保持する
        public float LowestAllyHpRatio { get; private set; } = 1f;
        // 攻撃力が最も高い敵。最優先で潰したい相手として扱う
        public PPBattleUnit HighestThreatEnemy { get; private set; }
        // パーティ全体の HP 割合。0～1 で保持する（LowestAllyHpRatio と同じ尺度）
        public float PartyHpRatio { get; private set; } = 0f;
        // 危機的状況かどうか。PPBattleRules.CrisisHpRatio を下回ると true
        public bool IsCrisis { get; private set; } = false;
        // ConditionedUnits の実体
        private readonly List<PPBattleUnit> mConditionedUnits = new();
        // ユニットごとのバトル中の見聞きを引く口。差し込まれていなければ null
        // 見聞きを持つのは思考ルーチン側だが、条件クラスが受け取るのはこのスナップショットなので中継する
        private Func<PPBattleUnit, PPUnitAIBlackboard> mBlackboardResolver;

        /* プロパティ */

        // ターゲット検索ノードが絞り込んだ対象候補。重複登録した分だけランダム選択で選ばれやすくなる
        // 直接書き換えず RegisterConditionedUnit / ResetConditionedUnits を通すこと
        public IReadOnlyList<PPBattleUnit> ConditionedUnits => mConditionedUnits;

        /* static 関数 */

        // 現在のパーティ状況を集計してスナップショットを生成する
        // 味方の HP 集計、敵の生存者・最低 HP・脅威度の探索、危機判定を 1 回で済ませる
        public static PPPartyAIContext Capture(PPBattleParty aParty, BattleContext aContext)
        {
            var snap = new PPPartyAIContext { Party = aParty, Context = aContext };

            float sumCur = 0f;
            float sumMax = 0f;
            float highestAllyRatio = float.MinValue;

            // 味方パーティの集計
            // 生存者を集めつつ、HP 合計と最低 HP 割合の持ち主を同時に求める
            foreach (var u in aParty.ActiveMembers)
            {
                if(u is not PPBattleUnit pp || !pp.IsAlive)
                    continue;
                snap.AliveMembers.Add(pp);

                sumCur += pp.Parameters.Hp.CurrentValue;
                sumMax += pp.Parameters.Hp.Max.CurrentValue;

                float ratio = HpRatio(pp);
                if (ratio < snap.LowestAllyHpRatio)
                {
                    snap.LowestAllyHpRatio = ratio;
                    snap.LowestHpRatioAlly = pp;
                }

                if (ratio > highestAllyRatio)
                {
                    highestAllyRatio = ratio;
                    snap.HighestHpRatioAlly = pp;
                }
            }

            snap.PartyHpRatio = sumMax > 0f ? sumCur / sumMax : 0f;

            // 敵パーティの集計。相手側は参照比較で判定する
            // 生存者を集めながら、最低 HP・最低 HP 割合・最大攻撃力の持ち主を同時に求める
            var opponent = ReferenceEquals(aParty, aContext.EnemyParty)
                ? aContext.AllyParty
                : aContext.EnemyParty;

            float lowestHp = float.MaxValue;
            float highestHp = float.MinValue;
            float lowestEnemyRatio = float.MaxValue;
            float highestAttack = float.MinValue;
            foreach (var e in opponent.GetAliveActiveMembers())
            {
                if(e is not PPBattleUnit pp || !pp.IsAlive)
                    continue;
                snap.AliveEnemies.Add(pp);

                float hp = e.Parameters.Hp.CurrentValue;
                if (hp < lowestHp)
                {
                    lowestHp = hp;
                    snap.LowestHpEnemy = pp;
                }

                if (hp > highestHp)
                {
                    highestHp = hp;
                    snap.HighestHpEnemy = pp;
                }

                float ratio = HpRatio(pp);
                if (ratio < lowestEnemyRatio)
                {
                    lowestEnemyRatio = ratio;
                    snap.LowestHpRatioEnemy = pp;
                }

                // 脅威度は攻撃力で測る。装備や状態異常込みの現在値を見る
                float attack = e.Parameters.Attack.CurrentValue;
                if (attack > highestAttack)
                {
                    highestAttack = attack;
                    snap.HighestThreatEnemy = pp;
                }
            }

            // 危機判定の閾値は拡張ルール側にしか無いため、差し込まれている場合のみ評価する
            if (aContext.Rules is PPBattleRules rule)
            {
                snap.IsCrisis = snap.LowestAllyHpRatio <= rule.CrisisHpRatio;
            }

            return snap;
        }

        // ユニットの HP 割合を 0～1 で求める。最大 HP が 0 以下の場合は 0 を返す
        public static float HpRatio(PPBattleUnit aUnit)
        {
            float max = aUnit.Parameters.Hp.Max.CurrentValue;
            return max <= 0f ? 0f : aUnit.Parameters.Hp.CurrentValue / max;
        }

        /* 初期化・接続 */

        // 見聞きを引く口を差し込む
        // aResolver : ユニットから見聞きを引く処理
        public void AttachBlackboardResolver(Func<PPBattleUnit, PPUnitAIBlackboard> aResolver)
            => mBlackboardResolver = aResolver;

        // 対象候補を 1 体登録する。aIsUnique が false なら同じユニットを何度でも積める
        // 積まれた回数がそのままランダム選択の重みになる
        public void RegisterConditionedUnit(PPBattleUnit aUnit, bool aIsUnique = false)
        {
            if (aUnit == null) return;
            if (aIsUnique && mConditionedUnits.Contains(aUnit)) return;

            mConditionedUnits.Add(aUnit);
        }

        /* 操作 */

        // 登録済みの対象候補を全て捨てる
        // 前の評価で積んだ候補を持ち越さないよう、ツリーを評価する前に必ず呼ぶ
        public void ResetConditionedUnits() => mConditionedUnits.Clear();

        /* 問い合わせ */

        // ユニットのバトル中の見聞きを引く
        public PPUnitAIBlackboard GetBlackboard(PPBattleUnit aUnit)
            => aUnit != null ? mBlackboardResolver?.Invoke(aUnit) : null;
    }
}
