/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPCriticalResolver.cs
 * @author hqrse
 * @date 2026/09/26
 * @brief きようさから会心率を求めるクリティカルリゾルバ
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 攻撃側のきようさから会心率を求めるクリティカルリゾルバ。PPBattleRules の既定
    // 会心率(%) = clamp(補正後きようさ × 0.25, 0, 100)、判定に使う確率 = clamp(補正後きようさ × 0.0025, 0, 1)
    // 補正後きようさ = 育成きようさ × S（スキル効果）× B（バフ・デバフ）で、Dexterity パラメータの CurrentValue にあたる
    // きようさ自体は 400 で頭打ちにせず、会心率の側だけを丸める（きようさ 400 以上は常に会心）
    // 乱数・倍率の扱いは基底の StandardCriticalResolver に任せ、発生率の求め方だけを差し替える
    // きようさを持たないユニット（PPBattleUnit 以外）は基底の固定発生率で判定する
    public class PPCriticalResolver : StandardCriticalResolver
    {
        // きようさ 1 あたりの会心確率（0～1）。計算は ToCriticalChance を使う（定数は仕様の参照用）
        public const float CriticalChancePerDexterity = 0.0025f;
        // きようさ 1 あたりの会心率（%）
        public const float CriticalPercentPerDexterity = 0.25f;

        // 補正後きようさから判定用の会心確率を求める
        // 0.0025 は float で正確に表せず、きようさ 400 でも 1 をわずかに下回るため、
        // 正確に表せる百分率（× 0.25）を経由して求める（400 → 100% → 1.0 ちょうど）
        // aDexterity : 補正後きようさ
        // return : 0～1 に丸めた会心確率
        public static float ToCriticalChance(float aDexterity)
            => Mathf.Clamp01(ToCriticalPercent(aDexterity) / 100f);

        // 補正後きようさから表示用の会心率（%）を求める
        // aDexterity : 補正後きようさ
        // return : 0～100 に丸めた会心率
        public static float ToCriticalPercent(float aDexterity)
            => Mathf.Clamp(aDexterity * CriticalPercentPerDexterity, 0f, 100f);

        // 攻撃側のきようさから会心確率を求める
        // きようさを持たないユニットは基底の固定発生率を返す
        protected override float ResolveCriticalChance(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext)
        {
            var dexterity = (aSource as PPBattleUnit)?.ExtraParameters?.Dexterity;
            if (dexterity == null)
            {
                return base.ResolveCriticalChance(aSource, aTarget, aInfo, aContext);
            }
            return ToCriticalChance(dexterity.CurrentValue);
        }
    }
}
