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
    // 時間の経過は「自分が何回思考したか（= 何回行動したか）」で数える。記録するのはストラテジストだけで、ノードと条件は読むだけにする
    public sealed class PPUnitAIBlackboard
    {
        /* 定数 */

        // 記録がまだ無いことを表す行動回数
        private const int NoAction = -1;

        /* 状態 */

        // 自分が思考した回数。行動が終わるたびに 1 回思考するため、行動回数と同じ意味で使う
        public int ActionCount { get; private set; }
        // 直近の 1 回の行動に掛かった秒数（バトル専用の時計）。まだ測れていなければ 0
        // クールタイムなど秒で決まるものと、行動回数を比べるときの換算に使う
        public float SecondsPerAction { get; private set; }
        // 直近に自分へダメージを与えたユニット
        // 反射ダメージのループを防ぐため発生元を持たないダメージがあり、その場合は更新しない
        public PPBattleUnit LastAttacker { get; private set; }
        // 直近にダメージを受けたときの行動回数。一度も受けていなければ NoAction
        public int LastDamagedAction { get; private set; } = NoAction;
        // 直近に自分が使ったスキルの定義。通常攻撃で行動した場合は null
        public PPSkillDefinition LastUsedSkill { get; private set; }
        // 直近に自分が狙った対象
        public PPBattleUnit LastTarget { get; private set; }
        // 直近に味方が倒されたときの行動回数。一度も倒れていなければ NoAction
        public int LastAllyDefeatedAction { get; private set; } = NoAction;
        // 狙いを固定している対象。固定していなければ null
        public PPBattleUnit FocusTarget { get; private set; }
        // 発生元ごとの累積被ダメージ量。誰を最も脅威とみなすかの判断材料になる
        // バトル通算の単純加算で、時間による減衰は行わない
        private readonly Dictionary<PPBattleUnit, float> mThreatByUnit = new();
        // 狙いの固定が切れる行動回数。無期限の場合は NoAction
        private int mFocusExpireAction = NoAction;
        // 前回思考したときの時刻（バトル専用の時計）。まだ思考していなければ負の値
        private float mLastThinkTime = -1f;

        /* 操作 */

        // 思考を 1 回始めることを記録する。行動回数を進め、前回の思考からの秒数を 1 回の行動に掛かった時間として覚える
        public void BeginThink(float aNow)
        {
            ActionCount++;
            if (mLastThinkTime >= 0f && aNow > mLastThinkTime)
            {
                SecondsPerAction = aNow - mLastThinkTime;
            }
            mLastThinkTime = aNow;
        }

        // 自分がダメージを受けたことを記録する
        public void RecordDamaged(PPBattleUnit aSource, float aAmount)
        {
            LastDamagedAction = ActionCount;
            // 発生元が無いダメージは「誰にやられたか」の材料にならないため、加害者の記録には使わない
            if (aSource == null) return;

            LastAttacker = aSource;
            mThreatByUnit.TryGetValue(aSource, out float total);
            mThreatByUnit[aSource] = total + aAmount;
        }

        // 自分が次に行う行動を決めたことを記録する
        public void RecordAction(PPSkillDefinition aSkill, PPBattleUnit aTarget)
        {
            LastUsedSkill = aSkill;
            if (aTarget != null) LastTarget = aTarget;
        }

        // 味方が倒されたことを記録する
        // 撃破通知には撃破者の情報が無いため、「誰に倒されたか」は残せない
        public void RecordAllyDefeated() => LastAllyDefeatedAction = ActionCount;

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

        // 狙いを固定する。aHoldActions 回の行動のあいだ続き、0 以下なら無期限
        public void SetFocus(PPBattleUnit aTarget, int aHoldActions)
        {
            if (aTarget == null) return;

            FocusTarget = aTarget;
            mFocusExpireAction = aHoldActions > 0 ? ActionCount + aHoldActions : NoAction;
        }

        // 狙いの固定を解除する
        public void ClearFocus()
        {
            FocusTarget = null;
            mFocusExpireAction = NoAction;
        }

        /* 問い合わせ */

        // 直近の指定回数の行動のあいだにダメージを受けたか
        public bool IsDamagedWithin(int aWithinActions)
            => LastDamagedAction != NoAction && ActionCount - LastDamagedAction <= aWithinActions;

        // 直近の指定回数の行動のあいだに味方が倒されたか
        public bool IsAllyDefeatedWithin(int aWithinActions)
            => LastAllyDefeatedAction != NoAction && ActionCount - LastAllyDefeatedAction <= aWithinActions;

        // そのユニットから受けた累積ダメージ量を引く
        public float GetThreat(PPBattleUnit aUnit)
            => aUnit != null && mThreatByUnit.TryGetValue(aUnit, out float total) ? total : 0f;

        // 今も有効な固定対象を引く
        // 対象が倒れた場合と保持する行動回数が過ぎた場合に解除し、以後は null を返す
        public PPBattleUnit ResolveFocus()
        {
            if (FocusTarget == null) return null;

            bool isExpired = !FocusTarget.IsAlive
                || (mFocusExpireAction != NoAction && ActionCount >= mFocusExpireAction);
            if (isExpired) ClearFocus();

            return FocusTarget;
        }
    }

    // ユニット 1 体分の AI の記憶。行動が確定したノードとその時点の行動回数を記録し、間隔を空ける判断や一度きりの判断に使う
    // 書き込むのは PPUnitAIStrategist だけで、ノードは読むだけにする。バトルをまたいで持ち越さない
    public sealed class PPUnitAIMemory
    {
        /* 状態 */

        // ノードごとに、最後に行動が確定したときの行動回数
        // 間隔を空ける・一度きり・ラッチはいずれもこの 1 つの記録から判定できる
        private readonly Dictionary<string, int> mFiredActions = new();

        /* 操作 */

        // ノードで行動が確定したことを記録する
        public void MarkFired(string aNodeId, int aActionCount)
        {
            if (string.IsNullOrEmpty(aNodeId)) return;

            mFiredActions[aNodeId] = aActionCount;
        }

        // 記憶を全て捨てる。バトルをまたいで持ち越さないために呼ぶ
        public void Clear() => mFiredActions.Clear();

        /* 問い合わせ */

        // そのノードで一度でも行動が確定したことがあるか
        public bool HasFired(string aNodeId)
            => !string.IsNullOrEmpty(aNodeId) && mFiredActions.ContainsKey(aNodeId);

        // そのノードで最後に確定したときの行動回数を引く
        public bool TryGetFiredAction(string aNodeId, out int aOutActionCount)
        {
            aOutActionCount = 0;
            return !string.IsNullOrEmpty(aNodeId) && mFiredActions.TryGetValue(aNodeId, out aOutActionCount);
        }
    }

    // 目当てのスキルまでの距離を「あと何回通常攻撃すれば届くか」で見積もるヘルパー
    // スキルゲージは通常攻撃で溜まるため、時間ではなく通常攻撃の回数で数える
    public static class PPUnitGaugeForecast
    {
        // 指定量のスキルゲージへ届くまでに必要な通常攻撃の回数を見積もる
        // 既に足りていれば 0、通常攻撃でスキルゲージが溜まらない場合は -1 を返す
        public static int EstimateAttacksToReach(PPBattleUnit aUnit, PPBattleRules aRules, float aRequired)
        {
            var skillGauge = aUnit.ExtraParameters.SkillGauge;
            if (PPGaugeUtility.CanPay(skillGauge, aRequired)) return 0;
            // 上限を超える量は、何回攻撃しても届かない
            if (aRequired > skillGauge.Max.CurrentValue + PPGaugeUtility.CompareEpsilon) return -1;

            float perAttack = aRules?.ResolveNormalAttackSkillGauge(aUnit) ?? 0f;
            if (perAttack <= 0f) return -1;

            float shortfall = aRequired - skillGauge.Current;
            return Mathf.CeilToInt((shortfall - PPGaugeUtility.CompareEpsilon) / perAttack);
        }
    }
}
