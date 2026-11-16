/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPTickActionCollector.cs
 * @author hqrse
 * @date 2026/08/26
 * @brief ティック内行動の収集・予約台帳・予約行動の供給元
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using CustomConsole;

namespace PPCore
{
    // 1 ティック分の行動を集めて実行順に並べる収集役
    //
    // 行動は決まった時点では実行せず、ティックの終わりにまとめて処理する
    // 敵 AI・プレイヤーの予約・未指定ユニットの通常攻撃を同じ土俵へ載せ、
    // 優先度と速度で並べ替えてから流すため、決まった順ではなく決めた内容で順序が決まる
    //
    // 予約の可否はユニットごとの仮押さえ台帳で判定する
    // ゲージが実際に減るのは実行時なので、台帳が無いと同じゲージを何度も当てにできてしまう
    public sealed class PPTickActionCollector
    {
        // プレイヤーが予約した行動
        private readonly List<PPPendingAction> mReservations = new();
        // 予約したぶんのゲージと行動回数を仮押さえする台帳
        private readonly PPUnitActionLedger mLedger = new();

        // 予約の判定に使う台帳。UI が発動可否の表示に使う
        public PPUnitActionLedger Ledger => mLedger;
        // 予約済みの行動
        public IReadOnlyList<PPPendingAction> Reservations => mReservations;

        // 予約内容が変わったときに発火する
        // 発動可否の表示や予約一覧を出している側が、購読して描画を更新する
        public event Action OnReservationChanged;

        // 予約を全て捨てる。ティックの行動を流し終えたら呼ぶ
        public void Clear()
        {
            if (mReservations.Count == 0 && mLedger.TotalActionCount == 0) return;

            mReservations.Clear();
            mLedger.Clear();
            OnReservationChanged?.Invoke();
        }

        // 予約を全て取り消す
        // プレイヤーが選び直したい場合の入口。ティック終了時の破棄と処理は同じ
        public void CancelAll()
        {
            if (mReservations.Count == 0) return;

            CustomConsoleLog.Log("Battle", $"予約を全て取り消しました（{mReservations.Count}件）。");
            Clear();
        }

        // 指定ユニットの予約を取り消す
        // aUnit : 取り消す対象のユニット
        // return : 取り消した件数
        public int CancelUnit(PPBattleUnit aUnit)
        {
            if (aUnit == null) return 0;

            int removed = mReservations.RemoveAll(x => ReferenceEquals(x.Unit, aUnit));
            if (removed == 0) return 0;

            RebuildLedger();
            CustomConsoleLog.Log("Battle", $"{aUnit.DisplayName}の予約を取り消しました（{removed}件）。");
            OnReservationChanged?.Invoke();
            return removed;
        }

        // 最後に積んだ予約を 1 件取り消す
        // 直前の選択だけをやり直したい場合に使う
        // return : 取り消せた場合 true
        public bool CancelLast()
        {
            if (mReservations.Count == 0) return false;

            var last = mReservations[^1];
            mReservations.RemoveAt(mReservations.Count - 1);
            RebuildLedger();
            CustomConsoleLog.Log("Battle", $"{last.Unit?.DisplayName}の予約を1件取り消しました。");
            OnReservationChanged?.Invoke();
            return true;
        }

        // 残っている予約から台帳を作り直す
        // 取り消しのたびに差分を引くと戻し忘れが起きるため、常に残りから組み直す
        private void RebuildLedger()
        {
            mLedger.Clear();
            foreach (var reservation in mReservations)
            {
                ReserveToLedger(reservation.Unit, reservation.Command);
            }
        }

        // その行動を予約できるかを判定する
        // 行動回数の空きと、既に予約した分を差し引いたゲージ残量の両方を見る
        // aUnit : 行動するユニット
        // aCommand : 予約したい行動
        // return : 予約できる場合 true
        public bool CanReserve(PPBattleUnit aUnit, BattleCommandBase aCommand)
        {
            if (aUnit == null || aCommand == null) return false;

            return aCommand switch
            {
                PPSkillCommand skillCommand => mLedger.CanReserveSkill(aUnit, ResolveSkillCost(skillCommand)),
                IPPBattleCommand attackCommand => mLedger.CanReserveCoin(aUnit, attackCommand.AttackCost),
                _ => mLedger.HasActionLeft(aUnit),
            };
        }

        // 行動を予約する。予約できない場合は何も積まずに false を返す
        // aUnit : 行動するユニット
        // aCommand : 予約する行動
        // return : 予約できた場合 true
        public bool TryReserve(PPBattleUnit aUnit, BattleCommandBase aCommand)
        {
            if (!CanReserve(aUnit, aCommand))
            {
                CustomConsoleLog.Warning("Battle",
                    $"{aUnit?.DisplayName} の行動は予約できませんでした（ゲージまたは行動回数が不足）。");
                return false;
            }

            ReserveToLedger(aUnit, aCommand);
            mReservations.Add(PPPendingAction.FromCommand(aUnit, aCommand));
            OnReservationChanged?.Invoke();
            return true;
        }

        // コマンドの種類に応じて台帳へ仮押さえする
        // 予約の追加と台帳の作り直しの双方から使うため、振り分けはここへ集約している
        // aUnit : 行動するユニット
        // aCommand : 仮押さえするコマンド
        private void ReserveToLedger(PPBattleUnit aUnit, BattleCommandBase aCommand)
        {
            switch (aCommand)
            {
                case PPSkillCommand skillCommand:
                    mLedger.ReserveSkill(aUnit, ResolveSkillCost(skillCommand));
                    break;
                case IPPBattleCommand attackCommand:
                    mLedger.ReserveCoin(aUnit, attackCommand.AttackCost);
                    break;
                default:
                    // 消費を伴わない行動でも、行動回数だけは数えないと無限に積めてしまう
                    mLedger.ReserveCoin(aUnit, 0f);
                    break;
            }
        }

        // 予約が無いユニットへ通常攻撃を積む
        // 何も指示していないユニットが棒立ちにならないようにするための既定行動で、
        // コインゲージが足りなければ何も積まない（そのユニットはそのティック待機になる）
        // aParty : 対象のパーティ
        // aContext : 対象の解決に使うバトルコンテキスト
        public void FillDefaultAttacks(PPBattleParty aParty, BattleContext aContext)
        {
            if (aParty == null) return;

            foreach (var member in aParty.ActiveMembers)
            {
                if (member is not PPBattleUnit unit || !unit.IsAlive) continue;

                // 行動回数が余っているあいだは通常攻撃で埋める
                while (mLedger.HasActionLeft(unit))
                {
                    var target = ResolveDefaultTarget(unit, aContext);
                    if (target == null) break;

                    var command = new PPAttackCommand(unit, new SingleEnemyResolver(target));
                    if (!mLedger.CanReserveCoin(unit, command.AttackCost)) break;

                    mLedger.ReserveCoin(unit, command.AttackCost);
                    mReservations.Add(PPPendingAction.FromCommand(unit, command));
                }
            }
        }

        // 予約と AI の計画を 1 つにまとめ、実行順に並べ替える
        // 並べ替えは全ての行動が揃ってから行う。どこかで先に並べてしまうと順序が混ざる
        // aContext : 並び替えに使うバトルコンテキスト
        // aPlans : AI が立てた計画。null の要素は読み飛ばす
        // return : 先に実行する順に並べた行動
        public List<PPPendingAction> CollectOrdered(BattleContext aContext, params PPPartyPlan[] aPlans)
        {
            var actions = new List<PPPendingAction>(mReservations);

            foreach (var plan in aPlans)
            {
                if (plan == null) continue;

                foreach (var assignment in plan.Assignments)
                {
                    actions.Add(PPPendingAction.FromCommand(assignment.Unit, assignment.Command));
                }
            }

            // 並べ方はルール側へ委譲する。差し替えたい場合は PPBattleRules.ActionOrderResolver を変える
            return aContext.Rules is PPBattleRules rules
                ? rules.ActionOrderResolver.ResolveOrder(actions, aContext)
                : actions;
        }

        // スキルの必要ゲージ量を引く。定義を解決できない場合は無コスト扱い
        // aCommand : 対象のスキルコマンド
        // return : 必要スキルゲージ量
        private static float ResolveSkillCost(PPSkillCommand aCommand)
            => (aCommand.Skill.SourceDefinition as PPSkillDefinition)?.SkillGaugeCost ?? 0f;

        // 既定の通常攻撃で狙う相手を決める。HP 割合が最も低い敵を選ぶ
        // aUnit : 攻撃するユニット
        // aContext : 相手パーティを引くバトルコンテキスト
        // return : 狙う相手。生存する敵が居なければ null
        private static PPBattleUnit ResolveDefaultTarget(PPBattleUnit aUnit, BattleContext aContext)
        {
            PPBattleUnit best = null;
            float bestRatio = float.MaxValue;

            foreach (var enemy in aContext.GetOpponentParty(aUnit.Side).GetAliveActiveMembers())
            {
                if (enemy is not PPBattleUnit ppEnemy) continue;

                float ratio = PPPartyAIContext.HpRatio(ppEnemy);
                if (best != null && ratio >= bestRatio) continue;

                best = ppEnemy;
                bestRatio = ratio;
            }
            return best;
        }
    }

    // 1 ティック分の行動を積むあいだ、ユニットごとのゲージと行動回数を仮押さえする台帳
    //
    // ゲージが実際に減るのは行動を実行する時点だが、実行前に複数の行動を積む都合上、
    // 「まだ払っていないが払う予定の分」を差し引いて判定しないと同じゲージを二重に当てにしてしまう
    // 大技を 2 回積んだのに 1 回分しか払えず 2 回目が空振りする、という状態を防ぐためのもの
    //
    // 敵 AI は 1 回の思考のあいだ、プレイヤーは 1 ティックの予約が消化されるまで、それぞれ同じ台帳を使う
    // 味方と敵でユニットが重ならないため、台帳を分けても判定が混ざることはない
    public sealed class PPUnitActionLedger
    {
        // ユニット 1 体分の仮押さえ内容
        private sealed class PPLedgerEntry
        {
            // 仮押さえ済みのスキルゲージ量
            public float SkillGauge;
            // 仮押さえ済みのコインゲージ量
            public float CoinGauge;
            // 積んだ行動の数
            public int ActionCount;
        }

        // ユニットごとの仮押さえ内容
        private readonly Dictionary<PPBattleUnit, PPLedgerEntry> mEntries = new();

        // 積んだ行動の総数
        public int TotalActionCount { get; private set; }

        // 台帳を空にする。ティックが切り替わったタイミングで呼ぶ
        public void Clear()
        {
            mEntries.Clear();
            TotalActionCount = 0;
        }

        // そのユニットが積んだ行動の数を返す
        // aUnit : 対象ユニット
        // return : 積んだ行動の数
        public int ReservedCount(PPBattleUnit aUnit)
            => mEntries.TryGetValue(aUnit, out var entry) ? entry.ActionCount : 0;

        // そのユニットがまだ行動を積めるかを返す
        // 1 ティックあたりの行動回数上限（バフ込み）と、積んだ数を突き合わせる
        // aUnit : 対象ユニット
        // return : まだ積める場合 true
        public bool HasActionLeft(PPBattleUnit aUnit)
            => aUnit != null && ReservedCount(aUnit) < aUnit.ResolveActionCount();

        // 仮押さえ分を差し引いたスキルゲージの残量
        // aUnit : 対象ユニット
        // return : 残量
        public float RemainingSkillGauge(PPBattleUnit aUnit)
            => aUnit.ExtraParameters.SkillGauge.Current - ResolveEntry(aUnit, false).SkillGauge;

        // 仮押さえ分を差し引いたコインゲージの残量
        // aUnit : 対象ユニット
        // return : 残量
        public float RemainingCoinGauge(PPBattleUnit aUnit)
            => aUnit.ExtraParameters.CoinGauge.Current - ResolveEntry(aUnit, false).CoinGauge;

        // スキルゲージを仮押さえできるかを判定する
        // 行動回数の空きも併せて見るため、これ 1 つで「あと 1 手積めるか」が判る
        // aUnit : 対象ユニット
        // aCost : 必要なスキルゲージ量
        // return : 積める場合 true
        public bool CanReserveSkill(PPBattleUnit aUnit, float aCost)
            => HasActionLeft(aUnit) && RemainingSkillGauge(aUnit) + PPGaugeUtility.CompareEpsilon >= aCost;

        // コインゲージを仮押さえできるかを判定する
        // aUnit : 対象ユニット
        // aCost : 必要なコインゲージ量
        // return : 積める場合 true
        public bool CanReserveCoin(PPBattleUnit aUnit, float aCost)
            => HasActionLeft(aUnit) && RemainingCoinGauge(aUnit) + PPGaugeUtility.CompareEpsilon >= aCost;

        // スキル 1 回分を仮押さえする
        // aUnit : 対象ユニット
        // aCost : 消費予定のスキルゲージ量
        public void ReserveSkill(PPBattleUnit aUnit, float aCost)
        {
            var entry = ResolveEntry(aUnit, true);
            entry.SkillGauge += Mathf.Max(0f, aCost);
            entry.ActionCount++;
            TotalActionCount++;
        }

        // 通常攻撃 1 回分を仮押さえする
        // aUnit : 対象ユニット
        // aCost : 消費予定のコインゲージ量
        public void ReserveCoin(PPBattleUnit aUnit, float aCost)
        {
            var entry = ResolveEntry(aUnit, true);
            entry.CoinGauge += Mathf.Max(0f, aCost);
            entry.ActionCount++;
            TotalActionCount++;
        }

        // ユニットの仮押さえ内容を引く
        // aUnit : 対象ユニット
        // aIsCreate : 未登録の場合に作るか。判定のみのときは作らない
        // return : 仮押さえ内容。作らない指定で未登録なら空の内容
        private PPLedgerEntry ResolveEntry(PPBattleUnit aUnit, bool aIsCreate)
        {
            if (mEntries.TryGetValue(aUnit, out var entry)) return entry;
            if (!aIsCreate) return new PPLedgerEntry();

            entry = new PPLedgerEntry();
            mEntries[aUnit] = entry;
            return entry;
        }
    }

    // まだ実行されていない行動を AI へ見せるための口
    //
    // 行動はティック終了時にまとめて積まれて実行されるため、
    // AI が思考している時点では BattleManager のコマンド列は空になっている
    // 「相手が次に何をしようとしているか」を条件で見るには、
    // 積まれる前の材料であるプレイヤーの予約と相手 AI の計画を直接読む必要がある
    //
    // 実装はバトルの進行役（PPBattleRunner など）が担う
    // 予約と計画の両方を握っているのがそこだけのため
    public interface IPPPendingActionSource
    {
        // 指定した陣営の、まだ実行されていない行動を列挙する
        // aSide : 調べる陣営
        // return : 実行待ちの行動。無ければ空
        IEnumerable<PPPendingAction> EnumeratePending(BattleSide aSide);
    }
}
