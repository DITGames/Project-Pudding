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
    // ユニット 1 体分の「バトル中に何が起きたか」の記録
    //
    // 判断ツリーは毎ティック現在の状況だけを見て決めるため、
    // 「誰に殴られたか」「さっき何を撃ったか」といった経緯は本来どこにも残らない
    // 反撃・かばう・狙いの継続といった判断はその経緯が無いと書けないため、ここへ集める
    //
    // 記録するのはストラテジストだけで、ノードと条件は読むだけにする
    // ノードの評価がバトルの状態を変えない、という設計上の約束を守るため
    //
    // ノードの発火履歴（クールダウン・一度きり）は PPUnitAIMemory が持つ
    // あちらが「ツリーのどこを通ったか」、こちらが「バトルで何をされたか」という住み分けで、
    // どちらもバトルをまたいで持ち越さない
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
        // aSource : ダメージの発生元。取得できない場合は null
        // aAmount : 受けたダメージ量
        // aTurnCount : 受けた時点のターン数
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
        // aSkill : 使ったスキルの定義。通常攻撃など定義を持たない行動なら null
        // aTarget : 狙った対象。対象を取らない行動なら null
        public void RecordAction(PPSkillDefinition aSkill, PPBattleUnit aTarget)
        {
            LastUsedSkill = aSkill;
            if (aTarget != null) LastTarget = aTarget;
        }

        // 味方が倒されたことを記録する
        // 撃破通知には撃破者の情報が無いため、「誰に倒されたか」は残せない
        // aTurnCount : 倒された時点のターン数
        public void RecordAllyDefeated(int aTurnCount) => LastAllyDefeatedTurn = aTurnCount;

        // 指定したターン数以内にダメージを受けたか
        // aTurnCount : 現在のターン数
        // aWithinTicks : さかのぼって見るティック数
        // return : その範囲でダメージを受けていれば true
        public bool IsDamagedWithin(int aTurnCount, int aWithinTicks)
            => LastDamagedTurn != NoTurn && aTurnCount - LastDamagedTurn <= aWithinTicks;

        // 指定したターン数以内に味方が倒されたか
        // aTurnCount : 現在のターン数
        // aWithinTicks : さかのぼって見るティック数
        // return : その範囲で味方が倒されていれば true
        public bool IsAllyDefeatedWithin(int aTurnCount, int aWithinTicks)
            => LastAllyDefeatedTurn != NoTurn && aTurnCount - LastAllyDefeatedTurn <= aWithinTicks;

        // そのユニットから受けた累積ダメージ量を引く
        // aUnit : 調べる相手
        // return : 累積ダメージ量。記録が無ければ 0
        public float GetThreat(PPBattleUnit aUnit)
            => aUnit != null && mThreatByUnit.TryGetValue(aUnit, out float total) ? total : 0f;

        // 最も多くのダメージを与えてきた相手を引く
        // 倒れた相手は狙う意味が無いため対象から外す
        // return : 該当する相手。記録が無ければ null
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
        // aTarget : 固定する対象
        // aTurnCount : 固定した時点のターン数
        // aHoldTicks : 保持するティック数。0 なら対象が倒れるまで無期限
        public void SetFocus(PPBattleUnit aTarget, int aTurnCount, int aHoldTicks)
        {
            if (aTarget == null) return;

            FocusTarget = aTarget;
            mFocusExpireTurn = aHoldTicks > 0 ? aTurnCount + aHoldTicks : NoTurn;
        }

        // 今も有効な固定対象を引く
        // 対象が倒れた場合と保持ティック数が過ぎた場合に解除し、以後は null を返す
        // aTurnCount : 現在のターン数
        // return : 固定対象。固定していない・解除済みなら null
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

    // ユニット 1 体分の AI の記憶
    //
    // 判断ツリーはステートレスで、毎ティック根から評価し直す
    // 「一度だけ」「しばらく間を空ける」といった時間をまたぐ判断はツリー側では表現できないため、
    // 行動が確定したノードとその時点のターン数をここへ記録し、次の評価で参照する
    //
    // 書き込むのは PPUnitAIStrategist だけで、ノードは読むだけにする
    // ノードの評価がバトルの状態を変えない、という設計上の約束を守るため
    //
    // 経過は BattleContext.TurnCount の差分で数える
    // 残ティック数を減らしていく方式にしないのは、1 ティックに複数回思考する設定では二重に消化されるため
    // 記憶はバトルをまたがない（ストラテジストがバトル開始時に作り直される）
    public sealed class PPUnitAIMemory
    {
        // ノードごとに、最後に行動が確定したときのターン数
        // クールダウン・一度きり・ラッチはいずれもこの 1 つの記録から判定できる
        private readonly Dictionary<string, int> mFiredTurns = new();

        // ノードで行動が確定したことを記録する
        // aNodeId : 対象ノードの ID
        // aTurnCount : 確定した時点のターン数
        public void MarkFired(string aNodeId, int aTurnCount)
        {
            if (string.IsNullOrEmpty(aNodeId)) return;

            mFiredTurns[aNodeId] = aTurnCount;
        }

        // そのノードで一度でも行動が確定したことがあるか
        // aNodeId : 対象ノードの ID
        // return : 確定したことがあれば true
        public bool HasFired(string aNodeId)
            => !string.IsNullOrEmpty(aNodeId) && mFiredTurns.ContainsKey(aNodeId);

        // そのノードで最後に確定したターン数を引く
        // aNodeId : 対象ノードの ID
        // aOutTurnCount : 最後に確定したターン数
        // return : 記録があれば true
        public bool TryGetFiredTurn(string aNodeId, out int aOutTurnCount)
        {
            aOutTurnCount = 0;
            return !string.IsNullOrEmpty(aNodeId) && mFiredTurns.TryGetValue(aNodeId, out aOutTurnCount);
        }

        // 記憶を全て捨てる。バトルをまたいで持ち越さないために呼ぶ
        public void Clear() => mFiredTurns.Clear();
    }

    // ユニットのゲージが数ティック先までにどこまで溜まるかを見積もるヘルパー
    //
    // 見積もりの土台は「手持ちのコインゲージで通常攻撃を何回撃てるか」
    // 通常攻撃はコインゲージを消費してスキルゲージを回復するため、
    // コインゲージの残量がそのまま「これから稼げるスキルゲージの上限」になる
    // 1 ティックあたりの行動回数で頭打ちにすることで、待ちティック数に応じた現実的な値になる
    //
    // プッシャー由来の新規コイン収入は予測に含めない（落ち方が読めないため、控えめな見積もりにしている）
    // 固有の落下物によるスキルゲージ回復など供給源が増えた場合は、
    // 追加供給量を aExtraSkillGaugePerTick として渡せば見積もりへ織り込める
    public static class PPUnitGaugeForecast
    {
        // 指定ティック数が経過した時点のスキルゲージ量を見積もる
        // aUnit : 対象ユニット
        // aRules : 通常攻撃の回復量を引く拡張ルール。null の場合は回復なしとして扱う
        // aTicks : 何ティック先を見るか。0 以下なら現在値をそのまま返す
        // aExtraSkillGaugePerTick : 通常攻撃以外の 1 ティックあたり供給量。既定は 0
        // return : 見積もりスキルゲージ量。上限を超えないよう丸める
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
        // 手持ちのコインゲージで払える回数と、行動回数で撃てる回数の小さいほうを採る
        // 通常攻撃コストが 0 のユニットはコインゲージで頭打ちにならないため、行動回数だけで決まる
        // aUnit : 対象ユニット
        // aTicks : 何ティック先を見るか
        // return : 見積もり攻撃回数
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
        // aUnit : 対象ユニット
        // aRules : 通常攻撃の回復量を引く拡張ルール
        // aRequired : 必要なスキルゲージ量
        // aMaxTicks : 何ティック先まで見るか
        // aExtraSkillGaugePerTick : 通常攻撃以外の 1 ティックあたり供給量。既定は 0
        // return : 到達までのティック数。届かない場合は -1
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
