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

        // ターゲット検索ノードが絞り込んだ対象候補
        // 同じユニットを重複して登録でき、重複した分だけランダム選択で選ばれやすくなる
        // 「複数の検索を重ねるほど優先される」を重み付けとして表現するための持ち方
        // 直接書き換えず RegisterConditionedUnit / ResetConditionedUnits を通すこと
        public IReadOnlyList<PPBattleUnit> ConditionedUnits => mConditionedUnits;

        // ConditionedUnits の実体
        private readonly List<PPBattleUnit> mConditionedUnits = new();

        // この思考で積んだ行動の仮押さえ台帳
        // 2 手目以降の判断で「1 手目が使う予定のゲージ」を差し引くために、条件・行動の双方から参照する
        // 未設定の場合は仮押さえを考慮しない（1 手だけ積む従来どおりの判定になる）
        public PPUnitActionLedger Ledger { get; private set; }

        // 仮押さえ台帳を結び付ける
        // aLedger : 結び付ける台帳
        public void AttachLedger(PPUnitActionLedger aLedger) => Ledger = aLedger;

        // HP 実数値が最も低い敵。とどめを狙う際の第一候補になる
        public PPBattleUnit LowestHpEnemy { get; private set; }
        // HP 割合が最も低い敵。AI の対象選択から引かれる
        public PPBattleUnit LowestHpRatioEnemy { get; private set; }
        // HP 割合が最も低い味方。回復スキルの対象になる
        public PPBattleUnit LowestHpRatioAlly { get; private set; }
        // 味方内で最も低い HP 割合。0～1 で保持する
        public float LowestAllyHpRatio { get; private set; } = 1f;
        // 攻撃力が最も高い敵。最優先で潰したい相手として扱う
        public PPBattleUnit HighestThreatEnemy { get; private set; }

        // パーティ全体の HP 割合。0～1 で保持する（LowestAllyHpRatio と同じ尺度）
        public float PartyHpRatio { get; private set; } = 0f;
        // 危機的状況かどうか。PPBattleRules.CrisisHpRatio を下回ると true
        public bool IsCrisis { get; private set; } = false;

        // 現在のパーティ状況を集計してスナップショットを生成する
        // 味方側の HP 集計と最低 HP 割合の探索、敵側の生存者列挙と最低 HP・脅威度の探索、
        // 危機判定、をこの 1 回で済ませる
        // aParty : 思考主体のパーティ
        // aContext : バトルコンテキスト
        // return : 集計済みのスナップショット
        public static PPPartyAIContext Capture(PPBattleParty aParty, BattleContext aContext)
        {
            var snap = new PPPartyAIContext { Party = aParty, Context = aContext };

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
            // 生存者を集めながら、最低 HP・最低 HP 割合・最大攻撃力の持ち主を同時に求める
            var opponent = ReferenceEquals(aParty, aContext.EnemyParty)
                ? aContext.AllyParty
                : aContext.EnemyParty;

            float lowestHp = float.MaxValue;
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
        // aUnit : 対象ユニット
        // return : HP 割合（0～1）
        public static float HpRatio(PPBattleUnit aUnit)
        {
            float max = aUnit.Parameters.Hp.Max.CurrentValue;
            return max <= 0f ? 0f : aUnit.Parameters.Hp.CurrentValue / max;
        }

        // 対象候補を 1 体登録する
        // aIsUnique が false のときは同じユニットを何度でも積める
        // 積まれた回数がそのままランダム選択の重みになるため、
        // 「複数の条件に合致したユニットほど狙われやすい」を重複登録で表現できる
        // aUnit : 登録するユニット。null なら何もしない
        // aIsUnique : 既に登録済みなら積まない場合 true
        public void RegisterConditionedUnit(PPBattleUnit aUnit, bool aIsUnique = false)
        {
            if (aUnit == null) return;
            if (aIsUnique && mConditionedUnits.Contains(aUnit)) return;

            mConditionedUnits.Add(aUnit);
        }

        // 登録済みの対象候補を全て捨てる
        // スナップショットはパーティ内の全ユニットで共有されるため、
        // 1 体分の思考を始める前に必ず呼んで、前のユニットの検索結果を持ち越さないようにする
        public void ResetConditionedUnits() => mConditionedUnits.Clear();
    }
}
