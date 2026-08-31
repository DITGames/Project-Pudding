/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitGaugeForecast.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief 数ティック先のゲージ量を見積もる
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
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
