/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitParameterVerifier.cs
 * @author hqrse
 * @date 2026/09/26
 * @brief ユニットパラメータ計算式の検証メニュー
 * =====================================*/

using CommandBattleCore;
using CustomConsole;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // ユニットパラメータ仕様（育成値 → BaseValue → CurrentValue、会心率、スキルゲージ上限）の検証メニュー
    // 実際の Parameter / PPBattleUnit / PPCriticalResolver を組み立て、仕様書 §7 の計算例をすべて照合する
    // 結果は Custom Console の "Battle_Verify" タグへケースごとの PASS / FAIL と最後の集計として出力する
    public static class PPUnitParameterVerifier
    {
        private const string LogTag = "Battle_Verify";
        // 浮動小数点の誤差を許容する幅
        private const float Tolerance = 0.001f;

        // 1 回の検証実行で集計する件数
        private static int mPassed;
        private static int mFailed;

        [MenuItem("Project-Pudding/Verify/Unit Parameters")]
        public static void Run()
        {
            mPassed = 0;
            mFailed = 0;
            CustomConsoleLog.Log(LogTag, "=== ユニットパラメータ検証 開始 ===");

            VerifyParameterStages();
            VerifyCriticalRate();
            VerifySkillGauge();
            VerifyResourceMaxChange();
            VerifyGrowthRounding();
            VerifyCriticalResolver();
            LogUnitAssets();

            string summary = $"=== ユニットパラメータ検証 終了: PASS {mPassed} / FAIL {mFailed} / 合計 {mPassed + mFailed} ===";
            if (mFailed == 0) CustomConsoleLog.Log(LogTag, summary);
            else CustomConsoleLog.Error(LogTag, summary);
        }

        // 育成値 → BaseValue → CurrentValue の 3 段階（仕様書 §7 の能力値の例）
        private static void VerifyParameterStages()
        {
            // 基礎 100・効果なし
            var unit = CreateUnit(100f, 20f, 100f);
            var attack = unit.Parameters.Attack;
            string id = ParameterSet.ParamIdAttack;
            Check("基礎100・効果なし: BaseValue", 100f, attack.BaseValue);
            Check("基礎100・効果なし: CurrentValue", 100f, attack.CurrentValue);

            // スキル +20%, +30% → Base 150
            var skillA = new object();
            var skillB = new object();
            unit.BaseParameters.AddSkillEffect(id, skillA, 0.2f);
            unit.BaseParameters.AddSkillEffect(id, skillB, 0.3f);
            Check("スキル+20%,+30%: S", 1.5f, unit.BaseParameters.GetSkillMultiplier(id));
            Check("スキル+20%,+30%: BaseValue", 150f, attack.BaseValue);
            Check("スキル+20%,+30%: CurrentValue", 150f, attack.CurrentValue);

            // さらにバフ +40%、デバフ -10% → Current 150 × 1.3 = 195
            var buff = new object();
            var debuff = new object();
            attack.AddModifier(new ParameterModifier(ParameterModifierType.Percent, buff, 0.4f));
            attack.AddModifier(new ParameterModifier(ParameterModifierType.Percent, debuff, -0.1f));
            Check("スキル+50%・バフ+40%,-10%: BaseValue", 150f, attack.BaseValue);
            Check("スキル+50%・バフ+40%,-10%: CurrentValue", 195f, attack.CurrentValue);

            // 一時効果を外す → Current 150、育成値は 100 のまま
            attack.RemoveModifiersFromSource(buff);
            attack.RemoveModifiersFromSource(debuff);
            Check("一時効果解除: CurrentValue", 150f, attack.CurrentValue);
            Check("一時効果解除: 育成値", 100f, unit.BaseParameters.GetGrowthValue(id));

            // スキル効果を外しても育成値から組み立て直されるだけで、二重掛けにならない
            unit.BaseParameters.RemoveSkillEffectsFromSource(skillA);
            unit.BaseParameters.RemoveSkillEffectsFromSource(skillB);
            Check("スキル効果解除: BaseValue", 100f, attack.BaseValue);
            Check("スキル効果解除: CurrentValue", 100f, attack.CurrentValue);

            // バフ合計 -120% → B = 0、Current 0（負にならない）
            var defense = CreateUnit(100f, 20f, 100f).Parameters.Defense;
            defense.AddModifier(new ParameterModifier(ParameterModifierType.Percent, new object(), -0.7f));
            defense.AddModifier(new ParameterModifier(ParameterModifierType.Percent, new object(), -0.5f));
            Check("バフ合計-120%: BaseValue", 100f, defense.BaseValue);
            Check("バフ合計-120%: CurrentValue", 0f, defense.CurrentValue);

            // スキル合計 -120% かつバフ合計 -150% → 両方 0（負同士を掛けて正にならない）
            var unit2 = CreateUnit(100f, 20f, 100f);
            var speed = unit2.Parameters.Speed;
            unit2.BaseParameters.AddSkillEffect(ParameterSet.ParamIdSpeed, new object(), -1.2f);
            speed.AddModifier(new ParameterModifier(ParameterModifierType.Percent, new object(), -1.5f));
            Check("スキル-120%・バフ-150%: S", 0f, unit2.BaseParameters.GetSkillMultiplier(ParameterSet.ParamIdSpeed));
            Check("スキル-120%・バフ-150%: BaseValue", 0f, speed.BaseValue);
            Check("スキル-120%・バフ-150%: CurrentValue", 0f, speed.CurrentValue);

            // 割合加算と既存方式の併用: (100 + 10) × (1 + 0.5) × 2 = 330、上書きは最優先
            var param = new Parameter(100f);
            param.AddModifier(new ParameterModifier(ParameterModifierType.Add, new object(), 10f));
            param.AddModifier(new ParameterModifier(ParameterModifierType.Percent, new object(), 0.5f));
            param.AddModifier(new ParameterModifier(ParameterModifierType.Multiply, new object(), 2f));
            Check("加算+割合加算+乗算の併用", 330f, param.CurrentValue);
            param.AddModifier(new ParameterModifier(ParameterModifierType.Override, new object(), 7f));
            Check("上書きが最優先", 7f, param.CurrentValue);

            // きようさがパラメータ ID でバフ対象として解決できること
            Check("きようさを ID で解決できる", true, unit.ResolveParameter(PPParameterSet.ParameterIdDexterity) == unit.ExtraParameters.Dexterity);
        }

        // 会心率（仕様書 §7 のきようさの例）
        private static void VerifyCriticalRate()
        {
            // きようさ 400・効果なし → 100%
            CheckCritical("きようさ400・効果なし", CreateUnit(100f, 400f, 100f), 100f);

            // きようさ 200・スキル +20%,+30%・バフ +40%,-10% → 200 × 1.5 × 1.3 × 0.25 = 97.5%
            var unit = CreateUnit(100f, 200f, 100f);
            string id = PPParameterSet.ParameterIdDexterity;
            unit.BaseParameters.AddSkillEffect(id, new object(), 0.2f);
            unit.BaseParameters.AddSkillEffect(id, new object(), 0.3f);
            unit.ExtraParameters.Dexterity.AddModifier(new ParameterModifier(ParameterModifierType.Percent, new object(), 0.4f));
            unit.ExtraParameters.Dexterity.AddModifier(new ParameterModifier(ParameterModifierType.Percent, new object(), -0.1f));
            Check("きようさ200・補正後きようさ", 390f, unit.ExtraParameters.Dexterity.CurrentValue);
            CheckCritical("きようさ200・スキル+50%・バフ+30%", unit, 97.5f);

            // きようさ 800・バフ -50% → 補正後 400 → 100%
            var unit2 = CreateUnit(100f, 800f, 100f);
            unit2.ExtraParameters.Dexterity.AddModifier(new ParameterModifier(ParameterModifierType.Percent, new object(), -0.5f));
            CheckCritical("きようさ800・バフ-50%", unit2, 100f);

            // きようさ 800・効果なし → 上限 100%（きようさ自体は 400 で頭打ちにしない）
            var unit3 = CreateUnit(100f, 800f, 100f);
            Check("きようさ800・効果なし: きようさは丸めない", 800f, unit3.ExtraParameters.Dexterity.CurrentValue);
            CheckCritical("きようさ800・効果なし", unit3, 100f);

            // 既定値 20 → 5%
            CheckCritical("きようさ20（既定値）", CreateUnit(100f, PPStatBlock.DefaultDexterity, 100f), 5f);
        }

        // スキルゲージ上限（仕様書 §7 のゲージの例）
        private static void VerifySkillGauge()
        {
            var unit = CreateUnit(100f, 20f, 100f);
            var gauge = unit.ExtraParameters.SkillGauge;
            Check("ゲージ基礎100: 上限", 100f, gauge.Max.CurrentValue);
            Check("ゲージ基礎100: 開始量は0", 0f, gauge.Current);

            unit.BaseParameters.AddSkillEffect(PPParameterSet.ParameterIdSkillGaugeMax, new object(), 0.2f);
            unit.BaseParameters.AddSkillEffect(PPParameterSet.ParameterIdSkillGaugeMax, new object(), 0.3f);
            Check("ゲージ基礎100・パッシブ+20%,+30%: 上限", 150f, gauge.Max.CurrentValue);
            Check("ゲージ上限上昇後も残量は変わらない", 0f, gauge.Current);
            Check("ゲージ上限はバフ対象に登録されない", true, unit.ResolveParameter(PPParameterSet.ParameterIdSkillGaugeMax) == null);
        }

        // 上限変化時の現在値の扱い（既存動作の維持）
        private static void VerifyResourceMaxChange()
        {
            var unit = CreateUnit(100f, 20f, 100f);
            var hp = unit.Parameters.Hp;
            Check("HPは満タンで開始", 100f, hp.Current);

            var passive = new object();
            unit.BaseParameters.AddSkillEffect(ParameterSet.ParamIdMaxHp, passive, -0.5f);
            Check("最大HP減少: 上限", 50f, hp.Max.CurrentValue);
            Check("最大HP減少: 現在値は上限へ切り詰め", 50f, hp.Current);

            unit.BaseParameters.RemoveSkillEffectsFromSource(passive);
            Check("最大HP増加: 上限", 100f, hp.Max.CurrentValue);
            Check("最大HP増加: 現在値は据え置き", 50f, hp.Current);
        }

        // 育成値の四捨五入（0.5 は 0 から遠い側）
        private static void VerifyGrowthRounding()
        {
            Check("四捨五入 2.5 → 3", 3f, PPUnitDefinition.RoundGrowthValue(2.5f));
            Check("四捨五入 3.5 → 4", 4f, PPUnitDefinition.RoundGrowthValue(3.5f));
            Check("四捨五入 2.4 → 2", 2f, PPUnitDefinition.RoundGrowthValue(2.4f));
            Check("四捨五入 -2.5 → -3", -3f, PPUnitDefinition.RoundGrowthValue(-2.5f));

            // 基礎 1.25 × 倍率 2 = 2.5 → 3（偶数丸めなら 2 になる）
            var curve = AnimationCurve.Constant(1f, 50f, 2f);
            Check("育成値 1.25×2 → 3", 3f, PPUnitDefinition.EvaluateGrowthValue(1.25f, curve, 10));
            // 倍率が 1 未満の曲線は 1 に丸める
            var weak = AnimationCurve.Constant(1f, 50f, 0.5f);
            Check("成長倍率は最低1", 20f, PPUnitDefinition.EvaluateGrowthValue(20f, weak, 10));
        }

        // クリティカルリゾルバの組み込み（乱数は固定値の供給元で与える）
        private static void VerifyCriticalResolver()
        {
            Check("PPBattleRules の既定は PPCriticalResolver", true, new PPBattleRules().CriticalResolver is PPCriticalResolver);

            var resolver = new PPCriticalResolver();

            // きようさ 20 → 5%。乱数 0.049 は発生、0.051 は不発
            var unit = CreateUnit(100f, 20f, 100f);
            unit.Random = new FixedRandomProvider(0.049f);
            Check("きようさ20・乱数0.049: 会心", true, resolver.Resolve(unit, unit, null, null).IsCritical);
            unit.Random = new FixedRandomProvider(0.051f);
            Check("きようさ20・乱数0.051: 会心しない", false, resolver.Resolve(unit, unit, null, null).IsCritical);

            // きようさ 400 → 100%。乱数の最大付近でも必ず発生
            var strong = CreateUnit(100f, 400f, 100f);
            strong.Random = new FixedRandomProvider(0.9999f);
            Check("きようさ400・乱数0.9999: 会心", true, resolver.Resolve(strong, strong, null, null).IsCritical);

            // きようさ 0 → 0%。乱数 0 でも発生しない
            var none = CreateUnit(100f, 0f, 100f);
            none.Random = new FixedRandomProvider(0f);
            Check("きようさ0・乱数0: 会心しない", false, resolver.Resolve(none, none, null, null).IsCritical);

            // PPBattleUnit 以外は基底の固定 10% にフォールバックする
            var plain = new BattleUnit("VerifyPlain", "VerifyPlain", new ParameterSet(100f, 100f, 100f, 100f));
            plain.Random = new FixedRandomProvider(0.099f);
            Check("非PPユニット・乱数0.099: 会心（固定10%）", true, resolver.Resolve(plain, plain, null, null).IsCritical);
            plain.Random = new FixedRandomProvider(0.101f);
            Check("非PPユニット・乱数0.101: 会心しない（固定10%）", false, resolver.Resolve(plain, plain, null, null).IsCritical);

            // 倍率は既存のまま
            Check("会心倍率は既存値", StandardCriticalResolver.DefaultCriticalMultiplier, resolver.Resolve(unit, unit, null, null).CriticalMultiplier);
        }

        // プロジェクト内のユニット定義のきようさを一覧する（参考情報。PASS / FAIL には数えない）
        private static void LogUnitAssets()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(PPUnitDefinition)))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<PPUnitDefinition>(path);
                if (definition == null) continue;
                float dexterity = definition.EvaluateDexterity(1);
                CustomConsoleLog.Verbose(LogTag,
                    $"[情報] {path}: きようさ(Lv1) {dexterity} / 会心率 {PPCriticalResolver.ToCriticalPercent(dexterity):0.##}% / スキルゲージ上限 {definition.ExpandStatBlock.SkillGaugeMax}",
                    definition);
            }
        }

        // 最大 HP・攻撃・防御・素早さがすべて aStat のユニットを組み立てる
        // 本番と同じ PPBattleUnit のコンストラクタを通すため、育成値テーブルも同じ経路で登録される
        private static PPBattleUnit CreateUnit(float aStat, float aDexterity, float aSkillGaugeMax)
            => new PPBattleUnit("Verify", "Verify",
                new ParameterSet(aStat, aStat, aStat, aStat),
                new PPParameterSet(5f, 1, aSkillGaugeMax, 100f, aDexterity),
                PPTypeAttribute.Normal);

        // ユニットのきようさ現在値から求めた会心率（%）と確率を照合する
        private static void CheckCritical(string aName, PPBattleUnit aUnit, float aExpectedPercent)
        {
            float dexterity = aUnit.ExtraParameters.Dexterity.CurrentValue;
            Check(aName + ": 会心率(%)", aExpectedPercent, PPCriticalResolver.ToCriticalPercent(dexterity));
            Check(aName + ": 会心確率", aExpectedPercent / 100f, PPCriticalResolver.ToCriticalChance(dexterity));
        }

        private static void Check(string aName, float aExpected, float aActual)
            => Report(aName, Mathf.Abs(aExpected - aActual) <= Tolerance, aExpected.ToString("0.####"), aActual.ToString("0.####"));

        private static void Check(string aName, bool aExpected, bool aActual)
            => Report(aName, aExpected == aActual, aExpected.ToString(), aActual.ToString());

        private static void Report(string aName, bool aPassed, string aExpected, string aActual)
        {
            if (aPassed)
            {
                mPassed++;
                CustomConsoleLog.Log(LogTag, $"PASS {aName}（期待 {aExpected} / 実際 {aActual}）");
            }
            else
            {
                mFailed++;
                CustomConsoleLog.Error(LogTag, $"FAIL {aName}（期待 {aExpected} / 実際 {aActual}）");
            }
        }

        // 常に同じ値を返す乱数供給元。判定の境界を確かめるために使う
        private sealed class FixedRandomProvider : IRandomProvider
        {
            private readonly float mValue;

            public FixedRandomProvider(float aValue) => mValue = aValue;

            public int NextInt(int aMaxExclusive) => 0;
            public int NextInt(int aMinInclusive, int aMaxExclusive) => aMinInclusive;
            public float NextFloat() => mValue;
            public float NextFloat(float aMinInclusive, float aMaxExclusive) => aMinInclusive + mValue * (aMaxExclusive - aMinInclusive);
            public bool NextBool(float aTrueChance) => mValue < aTrueChance;
        }
    }
}
