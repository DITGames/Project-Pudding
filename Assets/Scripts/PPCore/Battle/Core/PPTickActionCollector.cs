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
    // 1 ティック分の行動を集めて実行順に並べる収集役。敵 AI・プレイヤーの予約・未指定ユニットの通常攻撃を優先度と速度で並べ替える
    // 予約の可否はユニットごとの仮押さえ台帳で判定する
    public sealed class PPTickActionCollector
    {
        /* 状態 */

        // プレイヤーが予約した行動
        private readonly List<PPPendingAction> mReservations = new();
        // 予約したぶんのゲージと行動回数を仮押さえする台帳
        private readonly PPUnitActionLedger mLedger = new();

        /* プロパティ */

        // 予約の判定に使う台帳。UI が発動可否の表示に使う
        public PPUnitActionLedger Ledger => mLedger;
        // 予約済みの行動
        public IReadOnlyList<PPPendingAction> Reservations => mReservations;

        /* イベント */

        // 予約内容が変わったときに発火する
        // 発動可否の表示や予約一覧を出している側が、購読して描画を更新する
        public event Action OnReservationChanged;

        /* 操作 */

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

        // 行動を予約する。予約できない場合は何も積まずに false を返す
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

        // 予約が無いユニットへ通常攻撃を積む
        // コインゲージが足りなければ何も積まない（そのユニットはそのティック待機になる）
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

        // コマンドの種類に応じて台帳へ仮押さえする
        // 予約の追加と台帳の作り直しの双方から使うため、振り分けはここへ集約している
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

        /* 問い合わせ */

        // その行動を予約できるかを判定する
        // 行動回数の空きと、既に予約した分を差し引いたゲージ残量の両方を見る
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

        // 予約と AI の計画を 1 つにまとめ、実行順に並べ替える
        // 並べ替えは全ての行動が揃ってから行う。どこかで先に並べてしまうと順序が混ざる
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

        /* ヘルパー */

        // スキルの必要ゲージ量を引く。定義を解決できない場合は無コスト扱い
        private static float ResolveSkillCost(PPSkillCommand aCommand)
            => (aCommand.Skill.SourceDefinition as PPSkillDefinition)?.SkillGaugeCost ?? 0f;

        // 既定の通常攻撃で狙う相手を決める。HP 割合が最も低い敵を選ぶ
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
    // 実行前に複数の行動を積んでも、同じゲージを二重に当てにしないようにする
    public sealed class PPUnitActionLedger
    {
        /* 内部型 */

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

        /* 状態 */

        // 積んだ行動の総数
        public int TotalActionCount { get; private set; }
        // ユニットごとの仮押さえ内容
        private readonly Dictionary<PPBattleUnit, PPLedgerEntry> mEntries = new();

        /* 操作 */

        // 台帳を空にする。ティックが切り替わったタイミングで呼ぶ
        public void Clear()
        {
            mEntries.Clear();
            TotalActionCount = 0;
        }

        // そのユニットが積んだ行動の数を返す
        public int ReservedCount(PPBattleUnit aUnit)
            => mEntries.TryGetValue(aUnit, out var entry) ? entry.ActionCount : 0;

        // 仮押さえ分を差し引いたスキルゲージの残量
        public float RemainingSkillGauge(PPBattleUnit aUnit)
            => aUnit.ExtraParameters.SkillGauge.Current - ResolveEntry(aUnit, false).SkillGauge;

        // 仮押さえ分を差し引いたコインゲージの残量
        public float RemainingCoinGauge(PPBattleUnit aUnit)
            => aUnit.ExtraParameters.CoinGauge.Current - ResolveEntry(aUnit, false).CoinGauge;

        // スキル 1 回分を仮押さえする
        public void ReserveSkill(PPBattleUnit aUnit, float aCost)
        {
            var entry = ResolveEntry(aUnit, true);
            entry.SkillGauge += Mathf.Max(0f, aCost);
            entry.ActionCount++;
            TotalActionCount++;
        }

        // 通常攻撃 1 回分を仮押さえする
        public void ReserveCoin(PPBattleUnit aUnit, float aCost)
        {
            var entry = ResolveEntry(aUnit, true);
            entry.CoinGauge += Mathf.Max(0f, aCost);
            entry.ActionCount++;
            TotalActionCount++;
        }

        /* 問い合わせ */

        // そのユニットがまだ行動を積めるかを返す
        // 1 ティックあたりの行動回数上限（バフ込み）と、積んだ数を突き合わせる
        public bool HasActionLeft(PPBattleUnit aUnit)
            => aUnit != null && ReservedCount(aUnit) < aUnit.ResolveActionCount();

        // スキルゲージを仮押さえできるかを判定する
        // 行動回数の空きも併せて見るため、これ 1 つで「あと 1 手積めるか」が判る
        public bool CanReserveSkill(PPBattleUnit aUnit, float aCost)
            => HasActionLeft(aUnit) && RemainingSkillGauge(aUnit) + PPGaugeUtility.CompareEpsilon >= aCost;

        // コインゲージを仮押さえできるかを判定する
        public bool CanReserveCoin(PPBattleUnit aUnit, float aCost)
            => HasActionLeft(aUnit) && RemainingCoinGauge(aUnit) + PPGaugeUtility.CompareEpsilon >= aCost;

        // ユニットの仮押さえ内容を引く
        private PPLedgerEntry ResolveEntry(PPBattleUnit aUnit, bool aIsCreate)
        {
            if (mEntries.TryGetValue(aUnit, out var entry)) return entry;
            if (!aIsCreate) return new PPLedgerEntry();

            entry = new PPLedgerEntry();
            mEntries[aUnit] = entry;
            return entry;
        }
    }

    // まだ実行されていない行動（プレイヤーの予約と相手 AI の計画）を AI へ見せるための口
    // 実装はバトルの進行役（PPBattleRunner など）が担う
    public interface IPPPendingActionSource
    {
        // 指定した陣営の、まだ実行されていない行動を列挙する
        IEnumerable<PPPendingAction> EnumeratePending(BattleSide aSide);
    }
}
