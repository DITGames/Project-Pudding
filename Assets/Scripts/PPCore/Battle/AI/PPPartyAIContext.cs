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
    /// <summary>
    /// AI が思考を始める時点でのパーティ状況のスナップショット。
    /// <para>
    /// <see cref="Capture"/> で 1 回だけ集計し、以降の候補生成・スコアリング・条件評価は
    /// 全てこのスナップショットを参照する。思考の途中で状況が変わらないことを保証し、
    /// 生存メンバーの走査や最低 HP の探索を毎回やり直さずに済ませるのが狙い。
    /// </para>
    /// <para>
    /// AI 条件アセット（<see cref="PPPartyConditionValidator"/> 派生）の評価入力にもなる。
    /// </para>
    /// </summary>
    public sealed class PPPartyAIContext
    {
        /// <summary>思考主体のパーティ。</summary>
        public PPBattleParty Party { get; private set; }
        /// <summary>参照元のバトルコンテキスト。乱数やルールを引くのに使う。</summary>
        public BattleContext Context { get; private set; }

        /// <summary>スナップショット時点で生存しているアクティブな味方。</summary>
        public List<PPBattleUnit> AliveMembers { get; } = new();
        /// <summary>スナップショット時点で生存しているアクティブな敵。</summary>
        public List<PPBattleUnit> AliveEnemies { get; } = new();

        /// <summary>思考主体のパーティが持つリソースプール。</summary>
        public PPBattleResourcePool ResourcePool { get; private set; }
        /// <summary>指定属性のリソース現在値を取得するショートカット。</summary>
        /// <param name="a">対象の属性。</param>
        public float Current(PPTypeAttribute a) => ResourcePool.Current(a);

        /// <summary>HP 実数値が最も低い敵。とどめを狙う際の第一候補になる。</summary>
        public PPBattleUnit LowestHpEnemy { get; private set; }
        /// <summary>HP 割合が最も低い味方。回復スキルの対象になる。</summary>
        public PPBattleUnit LowestHpRatioAlly { get; private set; }
        /// <summary>味方内で最も低い HP 割合。0～1 で保持する。</summary>
        public float LowestAllyHpRatio { get; private set; } = 1f;

        /// <summary>
        /// パーティ全体の HP 割合。
        /// 条件アセット側の閾値設定に合わせて、0～1 ではなく 0～100 のパーセント値で保持する
        /// （0～1 の <see cref="LowestAllyHpRatio"/> とは尺度が違う点に注意）。
        /// </summary>
        public float PartyHpRatio { get; private set; } = 0f;
        /// <summary>危機的状況かどうか。<see cref="PPBattleRules.CrisisHpRatio"/> を下回ると true。</summary>
        public bool IsCrisis { get; private set; } = false;
        /// <summary>パーティの忍耐係数。AI が「待って溜める」判断をする際の許容 Tick 数に掛かる。</summary>
        public float PatienceCoefficient { get; private set; } = 0f;

        /// <summary>
        /// 現在のパーティ状況を集計してスナップショットを生成する。
        /// 味方側の HP 集計と最低 HP 割合の探索、敵側の生存者列挙と最低 HP の探索、
        /// 危機判定と忍耐係数の取り込み、をこの 1 回で済ませる。
        /// </summary>
        /// <param name="aParty">思考主体のパーティ。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>集計済みのスナップショット。</returns>
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

            // %変換
            snap.PartyHpRatio = sumMax > 0f ? sumCur / sumMax : 0f;
            snap.PartyHpRatio *= 100;

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

        /// <summary>
        /// ユニットの HP 割合を 0～1 で求める。最大 HP が 0 以下の場合は 0 を返す。
        /// </summary>
        /// <param name="aUnit">対象ユニット。</param>
        /// <returns>HP 割合（0～1）。</returns>
        public static float HpRatio(PPBattleUnit aUnit)
        {
            float max = aUnit.Parameters.Hp.Max.CurrentValue;
            return max <= 0f ? 0f : aUnit.Parameters.Hp.CurrentValue / max;
        }
    }
}
