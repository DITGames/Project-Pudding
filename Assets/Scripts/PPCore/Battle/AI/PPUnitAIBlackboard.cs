/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitAIBlackboard.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief ユニットAIの作業領域（ブラックボード・記憶・ゲージ予測）
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;

namespace PPCore
{
    // ユニット 1 体分の「バトル中に何をされたか」の記録。反撃・かばう・狙いの継続といった判断に使う
    // 記録するのはストラテジストだけで、ノードと条件は読むだけにする。バトルをまたいで持ち越さない
    public sealed class PPUnitAIBlackboard
    {
        // 記録がまだ無いことを表すターン数
        private const int NoTurn = -1;

        // 発生元ごとの累積被ダメージ量。誰を最も脅威とみなすかの判断材料になる
        // バトル通算の単純加算で、時間による減衰は行わない
        private readonly Dictionary<PPBattleUnit, float> mThreatByUnit = new();

        // 直近に自分へダメージを与えたユニット
        // 反射ダメージのループを防ぐため発生元を持たないダメージがあり、その場合は更新しない
        public PPBattleUnit LastAttacker { get; private set; }
        // 直近にダメージを受けたターン数。一度も受けていなければ NoTurn
        public int LastDamagedTurn { get; private set; } = NoTurn;
        // 直近に自分が使ったスキルの定義。通常攻撃で行動した場合は null
        public PPSkillDefinition LastUsedSkill { get; private set; }
        // 直近に自分が狙った対象
        public PPBattleUnit LastTarget { get; private set; }
        // 直近に味方が倒されたターン数。一度も倒れていなければ NoTurn
        public int LastAllyDefeatedTurn { get; private set; } = NoTurn;

        // 狙いを固定している対象。固定していなければ null
        public PPBattleUnit FocusTarget { get; private set; }
        // 狙いの固定が切れるターン数。無期限の場合は NoTurn
        private int mFocusExpireTurn = NoTurn;

        // 自分がダメージを受けたことを記録する
        public void RecordDamaged(PPBattleUnit aSource, float aAmount, int aTurnCount)
        {
            LastDamagedTurn = aTurnCount;
            // 発生元が無いダメージは「誰にやられたか」の材料にならないため、加害者の記録には使わない
            if (aSource == null) return;

            LastAttacker = aSource;
            mThreatByUnit.TryGetValue(aSource, out float total);
            mThreatByUnit[aSource] = total + aAmount;
        }

        // 自分が行動したことを記録する
        public void RecordAction(PPSkillDefinition aSkill, PPBattleUnit aTarget)
        {
            LastUsedSkill = aSkill;
            if (aTarget != null) LastTarget = aTarget;
        }

        // 味方が倒されたことを記録する
        // 撃破通知には撃破者の情報が無いため、「誰に倒されたか」は残せない
        public void RecordAllyDefeated(int aTurnCount) => LastAllyDefeatedTurn = aTurnCount;

        // 指定したターン数以内にダメージを受けたか
        public bool IsDamagedWithin(int aTurnCount, int aWithinTicks)
            => LastDamagedTurn != NoTurn && aTurnCount - LastDamagedTurn <= aWithinTicks;

        // 指定したターン数以内に味方が倒されたか
        public bool IsAllyDefeatedWithin(int aTurnCount, int aWithinTicks)
            => LastAllyDefeatedTurn != NoTurn && aTurnCount - LastAllyDefeatedTurn <= aWithinTicks;

        // そのユニットから受けた累積ダメージ量を引く
        public float GetThreat(PPBattleUnit aUnit)
            => aUnit != null && mThreatByUnit.TryGetValue(aUnit, out float total) ? total : 0f;

        // 最も多くのダメージを与えてきた相手を引く
        // 倒れた相手は狙う意味が無いため対象から外す
        public PPBattleUnit MostThreateningUnit()
        {
            PPBattleUnit found = null;
            float highest = 0f;
            foreach (var pair in mThreatByUnit)
            {
                if (pair.Key == null || !pair.Key.IsAlive || pair.Value <= highest) continue;

                highest = pair.Value;
                found = pair.Key;
            }
            return found;
        }

        // 狙いを固定する
        public void SetFocus(PPBattleUnit aTarget, int aTurnCount, int aHoldTicks)
        {
            if (aTarget == null) return;

            FocusTarget = aTarget;
            mFocusExpireTurn = aHoldTicks > 0 ? aTurnCount + aHoldTicks : NoTurn;
        }

        // 今も有効な固定対象を引く
        // 対象が倒れた場合と保持ティック数が過ぎた場合に解除し、以後は null を返す
        public PPBattleUnit ResolveFocus(int aTurnCount)
        {
            if (FocusTarget == null) return null;

            bool isExpired = !FocusTarget.IsAlive
                || (mFocusExpireTurn != NoTurn && aTurnCount >= mFocusExpireTurn);
            if (isExpired) ClearFocus();

            return FocusTarget;
        }

        // 狙いの固定を解除する
        public void ClearFocus()
        {
            FocusTarget = null;
            mFocusExpireTurn = NoTurn;
        }
    }

    // ユニット 1 体分の AI の記憶。行動が確定したノードとその時点のターン数を記録し、クールダウンや一度きりの判断に使う
    // 書き込むのは PPUnitAIStrategist だけで、ノードは読むだけにする。バトルをまたいで持ち越さない
    public sealed class PPUnitAIMemory
    {
        // ノードごとに、最後に行動が確定したときのターン数
        // クールダウン・一度きり・ラッチはいずれもこの 1 つの記録から判定できる
        private readonly Dictionary<string, int> mFiredTurns = new();

        // ノードで行動が確定したことを記録する
        public void MarkFired(string aNodeId, int aTurnCount)
        {
            if (string.IsNullOrEmpty(aNodeId)) return;

            mFiredTurns[aNodeId] = aTurnCount;
        }

        // そのノードで一度でも行動が確定したことがあるか
        public bool HasFired(string aNodeId)
            => !string.IsNullOrEmpty(aNodeId) && mFiredTurns.ContainsKey(aNodeId);

        // そのノードで最後に確定したターン数を引く
        public bool TryGetFiredTurn(string aNodeId, out int aOutTurnCount)
        {
            aOutTurnCount = 0;
            return !string.IsNullOrEmpty(aNodeId) && mFiredTurns.TryGetValue(aNodeId, out aOutTurnCount);
        }

        // 記憶を全て捨てる。バトルをまたいで持ち越さないために呼ぶ
        public void Clear() => mFiredTurns.Clear();
    }

    // ユニットのゲージが数ティック先までにどこまで溜まるかを見積もるヘルパー
    // 手持ちのコインゲージで撃てる通常攻撃の回数を土台にし、プッシャー由来の新規収入は含めない
    public static class PPUnitGaugeForecast
    {
        // 指定ティック数が経過した時点のスキルゲージ量を見積もる
        public static float EstimateSkillGauge(PPBattleUnit aUnit, PPBattleRules aRules, int aTicks,
            float aExtraSkillGaugePerTick = 0f)
        {
            var skillGauge = aUnit.ExtraParameters.SkillGauge;
            if (aTicks <= 0) return skillGauge.Current;

            float gained = EstimateAttackCount(aUnit, aTicks) * (aRules?.NormalAttackSkillGaugeRecover ?? 0f)
                         + Mathf.Max(0f, aExtraSkillGaugePerTick) * aTicks;

            return Mathf.Min(skillGauge.Current + gained, skillGauge.Max.CurrentValue);
        }

        // 指定ティック数の間に通常攻撃を何回撃てるかを見積もる
        // コインゲージで払える回数と、行動回数で撃てる回数の小さいほうを採る
        public static int EstimateAttackCount(PPBattleUnit aUnit, int aTicks)
        {
            if (aTicks <= 0) return 0;

            int byActions = aUnit.ResolveActionCount() * aTicks;
            float attackCost = aUnit.ExtraParameters.Get(PPParameterSet.ParameterIdAttackCost)?.CurrentValue ?? 0f;
            if (attackCost <= 0f) return byActions;

            int byCoin = Mathf.FloorToInt(aUnit.ExtraParameters.CoinGauge.Current / attackCost);
            return Mathf.Min(byActions, byCoin);
        }

        // 指定量のスキルゲージへ到達するまでに掛かるティック数を見積もる
        // 1 ティックずつ前へ進めて最初に届いたところを返す。上限までに届かなければ -1 を返す
        public static int EstimateTicksToReach(PPBattleUnit aUnit, PPBattleRules aRules, float aRequired,
            int aMaxTicks, float aExtraSkillGaugePerTick = 0f)
        {
            if (PPGaugeUtility.CanPay(aUnit.ExtraParameters.SkillGauge, aRequired)) return 0;

            for (int tick = 1; tick <= aMaxTicks; tick++)
            {
                float predicted = EstimateSkillGauge(aUnit, aRules, tick, aExtraSkillGaugePerTick);
                if (predicted + PPGaugeUtility.CompareEpsilon >= aRequired) return tick;
            }
            return -1;
        }
    }
}
