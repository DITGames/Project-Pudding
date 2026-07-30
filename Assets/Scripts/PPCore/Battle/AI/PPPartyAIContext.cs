/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyAIContext.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief AIの戦略評価用コンテキスト
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    // AI が思考を始める時点でのパーティ状況のスナップショット
    // Capture で 1 回だけ集計し、以降の候補生成・スコアリング・条件評価は
    // 全てこのスナップショットを参照する。思考の途中で状況が変わらないことを保証し、
    // 生存メンバーの走査や最低 HP の探索を毎回やり直さずに済ませるのが狙い
    // AI 条件アセット（PPPartyConditionValidator 派生）の評価入力にもなる
    public sealed class PPPartyAIContext
    {
        // 思考主体のパーティ
        public PPBattleParty Party { get; private set; }
        // 参照元のバトルコンテキスト。乱数やルールを引くのに使う
        public BattleContext Context { get; private set; }

        // スナップショット時点で生存しているアクティブな味方
        public List<PPBattleUnit> AliveMembers { get; } = new();
        // スナップショット時点で生存しているアクティブな敵
        public List<PPBattleUnit> AliveEnemies { get; } = new();

        // 思考主体のパーティが持つリソースプール
        public PPBattleResourcePool ResourcePool { get; private set; }
        // 指定属性のリソース現在値を取得するショートカット
        // a : 対象の属性
        public float Current(PPTypeAttribute a) => ResourcePool.Current(a);

        // HP 実数値が最も低い敵。とどめを狙う際の第一候補になる
        public PPBattleUnit LowestHpEnemy { get; private set; }
        // HP 割合が最も低い味方。回復スキルの対象になる
        public PPBattleUnit LowestHpRatioAlly { get; private set; }
        // 味方内で最も低い HP 割合。0～1 で保持する
        public float LowestAllyHpRatio { get; private set; } = 1f;

        // パーティ全体の HP 割合。0～1 で保持する（LowestAllyHpRatio と同じ尺度）
        public float PartyHpRatio { get; private set; } = 0f;
        // 危機的状況かどうか。PPBattleRules.CrisisHpRatio を下回ると true
        public bool IsCrisis { get; private set; } = false;
        // パーティの忍耐係数。AI が「待って溜める」判断をする際の許容 Tick 数に掛かる
        public float PatienceCoefficient { get; private set; } = 0f;

        // 現在のパーティ状況を集計してスナップショットを生成する
        // 味方側の HP 集計と最低 HP 割合の探索、敵側の生存者列挙と最低 HP の探索、
        // 危機判定と忍耐係数の取り込み、をこの 1 回で済ませる
        // aParty : 思考主体のパーティ
        // aContext : バトルコンテキスト
        // return : 集計済みのスナップショット
        public static PPPartyAIContext Capture(PPBattleParty aParty, BattleContext aContext)
        {
            var snap = new PPPartyAIContext { Party = aParty, Context = aContext };
            snap.ResourcePool = aParty.ResourcePool;

            float sumCur = 0f;
            float sumMax = 0f;

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
            }

            snap.PartyHpRatio = sumMax > 0f ? sumCur / sumMax : 0f;

            // 敵パーティの集計
            // 自分がどちら側かで相手パーティが変わるため、参照比較で判定する
            var opponent = ReferenceEquals(aParty, aContext.EnemyParty)
                ? aContext.AllyParty
                : aContext.EnemyParty;

            float lowestHp = float.MaxValue;
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
            }

            // 危機判定の閾値は拡張ルール側にしか無いため、差し込まれている場合のみ評価する
            if (aContext.Rules is PPBattleRules rule)
            {
                snap.IsCrisis = snap.LowestAllyHpRatio <= rule.CrisisHpRatio;
            }
            snap.PatienceCoefficient = aParty.PatienceCoefficient;

            return snap;
        }

        // ユニットの HP 割合を 0～1 で求める。最大 HP が 0 以下の場合は 0 を返す
        // aUnit : 対象ユニット
        // return : HP 割合（0～1）
        public static float HpRatio(PPBattleUnit aUnit)
        {
            float max = aUnit.Parameters.Hp.Max.CurrentValue;
            return max <= 0f ? 0f : aUnit.Parameters.Hp.CurrentValue / max;
        }
    }
}
