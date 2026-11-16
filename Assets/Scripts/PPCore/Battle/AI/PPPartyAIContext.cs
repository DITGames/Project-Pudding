/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPartyAIContext.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief パーティ状況のスナップショットとリソース増加トレンド
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
        // この思考で積んだ行動の仮押さえ台帳。2 手目以降の判断で 1 手目が使う予定のゲージを差し引く
        // 未設定の場合は仮押さえを考慮しない
        public PPUnitActionLedger Ledger { get; private set; }
        // 実行待ちの行動の供給元。差し込まれていなければ null
        public IPPPendingActionSource PendingSource { get; private set; }
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

        /* 生成 */

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

        /* 初期化・接続 */

        // 仮押さえ台帳を結び付ける
        // aLedger : 結び付ける台帳
        public void AttachLedger(PPUnitActionLedger aLedger) => Ledger = aLedger;

        // 見聞きを引く口を差し込む
        // aResolver : ユニットから見聞きを引く処理
        public void AttachBlackboardResolver(Func<PPBattleUnit, PPUnitAIBlackboard> aResolver)
            => mBlackboardResolver = aResolver;

        // 実行待ちの行動の供給元を差し込む
        // aSource : 実行待ちの行動の供給元
        public void AttachPendingSource(IPPPendingActionSource aSource) => PendingSource = aSource;

        // 対象候補を 1 体登録する。aIsUnique が false なら同じユニットを何度でも積める
        // 積まれた回数がそのままランダム選択の重みになる
        public void RegisterConditionedUnit(PPBattleUnit aUnit, bool aIsUnique = false)
        {
            if (aUnit == null) return;
            if (aIsUnique && mConditionedUnits.Contains(aUnit)) return;

            mConditionedUnits.Add(aUnit);
        }

        /* 操作 */

        // ユニットの HP 割合を 0～1 で求める。最大 HP が 0 以下の場合は 0 を返す
        public static float HpRatio(PPBattleUnit aUnit)
        {
            float max = aUnit.Parameters.Hp.Max.CurrentValue;
            return max <= 0f ? 0f : aUnit.Parameters.Hp.CurrentValue / max;
        }

        // 登録済みの対象候補を全て捨てる
        // スナップショットは全ユニットで共有されるため、1 体分の思考を始める前に必ず呼ぶ
        public void ResetConditionedUnits() => mConditionedUnits.Clear();

        /* 問い合わせ */

        // ユニットのバトル中の見聞きを引く
        public PPUnitAIBlackboard GetBlackboard(PPBattleUnit aUnit)
            => aUnit != null ? mBlackboardResolver?.Invoke(aUnit) : null;
    }

    // リソースの増加ペースを記録するトラッカー。直近サンプルの増分を平均して、目当てのスキルまでの待ち時間を見積もる
    // 消費で残量が減った場合は増分 0 として扱う
    public sealed class PPIncomTrendTracker
    {
        /* 状態 */

        // 直近の増分の履歴。古いものから順に捨てられる
        private readonly Queue<float> mRecentGains = new();
        // 前回サンプリング時のリソース量。初回は増分を計算できないため null
        private float? mLastLevel;

        /* プロパティ */

        // 直近サンプルの平均増加量（1 ティックあたり）
        // サンプルが無ければ 0 を返し、待機判定側では「増加が見込めない」と扱われる
        public float AverageRecentGainPerTick
            => mRecentGains.Count == 0 ? 0f : mRecentGains.Average();

        /* 操作 */

        // 現在のリソース量をサンプリングし、前回からの増分を履歴へ積む
        // 履歴が指定件数を超えた分は古い方から捨てる
        public void Sample(float aCurrentLevel, int aSampleCount)
        {
            if (mLastLevel.HasValue)
            {
                mRecentGains.Enqueue(Math.Max(0f, aCurrentLevel - mLastLevel.Value));
            }
            mLastLevel = aCurrentLevel;

            while (mRecentGains.Count > Math.Max(1, aSampleCount))
            {
                mRecentGains.Dequeue();
            }
        }
    }
}
