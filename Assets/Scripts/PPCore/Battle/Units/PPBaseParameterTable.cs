/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBaseParameterTable.cs
 * @author hqrse
 * @date 2026/09/26
 * @brief 基礎パラメータ（成長テーブル・ステータスブロック・能力値の定義）
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;

namespace PPCore
{
    // ユニットの能力値を「育成値 → BaseValue（育成値 × スキル効果）→ CurrentValue（BaseValue × バフ・デバフ）」の 3 段階で扱うテーブル
    // 再計算のたびに育成値から組み立て直す。効果値は割合で持ち（+20% は 0.2）、合計してから倍率にする
    public class PPBaseParameterTable
    {
        // 1 件分のスキル効果値。付与元は解除時の照合に使う
        protected readonly struct SkillEffectValue
        {
            // 付与元。参照比較で照合する
            public readonly object Source;
            // 効果値（割合）
            public readonly float Value;

            public SkillEffectValue(object aSource, float aValue)
            {
                Source = aSource;
                Value = aValue;
            }
        }

        // パラメータ 1 つ分の登録内容
        protected class Entry
        {
            // BaseValue を書き込む先のパラメータ
            public Parameter Target;
            // 育成値。スキル効果を掛ける前の値
            public float GrowthValue;
            // 掛かっているスキル効果値の一覧
            public readonly List<SkillEffectValue> SkillEffects = new();
        }

        // パラメータ ID → 登録内容
        protected readonly Dictionary<string, Entry> mEntries = new();

        // 登録済みのパラメータ ID 一覧
        public IEnumerable<string> Ids => mEntries.Keys;

        // パラメータを登録する。登録時点の BaseValue をそのまま育成値として扱う
        // ランタイムユニットの生成直後（スキル効果が 1 つも掛かっていない状態）で呼ぶ前提
        public void Register(string aId, Parameter aTarget)
        {
            if (aTarget == null) return;
            Register(aId, aTarget, aTarget.BaseValue);
        }

        // 育成値を明示してパラメータを登録し、BaseValue を組み立て直す
        public void Register(string aId, Parameter aTarget, float aGrowthValue)
        {
            if (aTarget == null) return;
            mEntries[aId] = new Entry { Target = aTarget, GrowthValue = aGrowthValue };
            Recalculate(aId);
        }

        // 指定 ID が登録済みかを返す
        public bool Contains(string aId) => mEntries.ContainsKey(aId);

        // 育成値を取得する
        public float GetGrowthValue(string aId)
            => mEntries.TryGetValue(aId, out var entry) ? entry.GrowthValue : 0f;

        // 育成値を差し替えて BaseValue を組み立て直す。レベルアップなど恒久的な成長で使う
        public void SetGrowthValue(string aId, float aGrowthValue)
        {
            if (!mEntries.TryGetValue(aId, out var entry)) return;
            entry.GrowthValue = aGrowthValue;
            Recalculate(aId);
        }

        // スキル効果値を追加して BaseValue を組み立て直す
        public void AddSkillEffect(string aId, object aSource, float aValue)
        {
            if (!mEntries.TryGetValue(aId, out var entry)) return;
            entry.SkillEffects.Add(new SkillEffectValue(aSource, aValue));
            Recalculate(aId);
        }

        // 指定した付与元のスキル効果値をすべて外し、影響のあったパラメータの BaseValue を組み立て直す
        // aSource : 付与元
        public void RemoveSkillEffectsFromSource(object aSource)
        {
            foreach (var pair in mEntries)
            {
                if (pair.Value.SkillEffects.RemoveAll(e => ReferenceEquals(e.Source, aSource)) > 0)
                {
                    Recalculate(pair.Key);
                }
            }
        }

        // 指定パラメータに掛かっているスキル効果値の合計
        public float GetSkillEffectSum(string aId)
        {
            if (!mEntries.TryGetValue(aId, out var entry)) return 0f;
            float sum = 0f;
            foreach (var effect in entry.SkillEffects) sum += effect.Value;
            return sum;
        }

        // スキル効果の倍率 S = max(0, 1 + 効果値の合計)
        public float GetSkillMultiplier(string aId)
            => Parameter.RatioSumToMultiplier(GetSkillEffectSum(aId));

        // 育成値とスキル効果値から BaseValue を組み立て直し、CurrentValue も再計算する
        // 上限系（最大 HP・スキルゲージ上限）の場合、上限が下がれば ResourceParameter 側で残量が切り詰められる
        public virtual void Recalculate(string aId)
        {
            if (!mEntries.TryGetValue(aId, out var entry)) return;
            entry.Target.SetBaseValue(entry.GrowthValue * GetSkillMultiplier(aId));
        }

        // 登録済みのすべてのパラメータを組み立て直す
        public void RecalculateAll()
        {
            foreach (var id in mEntries.Keys)
            {
                Recalculate(id);
            }
        }
    }

    // ユニット定義がインスペクタ上で基礎ステータスを設定するための構造体
    // ランタイムの ParameterSet と違い修飾子の仕組みを持たない、素の数値の入れ物
    [Serializable]
    public struct StatBlock
    {
        [Label("最大HP")]
        public float MaxHP;
        [Label("攻撃力")]
        public float Attack;
        [Label("防御力")]
        public float Defense;
        // 行動順の決定に使う
        [Label("素早さ")]
        public float Speed;
    }

    // ユニット定義がインスペクタ上で設定する追加ステータス（StatBlock に対する差分）
    // 構造体はフィールド初期化子を持てないため、既定値の定数もここに集約する
    [Serializable]
    public struct PPStatBlock
    {
        // 新規作成したユニット定義のきようさ既定値（会心率 5%）
        public const float DefaultDexterity = 20f;
        // 新規作成したユニット定義のスキルゲージ上限の既定値
        public const float DefaultSkillGaugeMax = 100f;

        [Label("通常攻撃コスト")]
        public float AttackCost;
        // 1 ティックあたりに行動できる回数。バフで増減しうるため、ここでは初期値だけを持つ
        [Label("行動回数上限")]
        public int ActionCount;
        // スキルゲージの上限。スキルの必要スキルゲージ量はこの範囲内で設定する
        [Label("スキルゲージ上限")]
        public float SkillGaugeMax;
        // コインゲージの上限。通常攻撃コストはこの範囲内で設定する
        [Label("コインゲージ上限")]
        public float CoinGaugeMax;
        // 会心率の元になる基礎能力（レベル 1 時点の値）。PPUnitDefinition の成長曲線でレベル成長する
        // 会心率(%) = 補正後きようさ × 0.25（0～100 に丸める）。既定値 20 で会心率 5%
        [Label(PPUnitAbilityDefinition.NameDexterity)]
        public float Dexterity;
    }

    // ユニットの基礎能力（HP・ちから・まもり・はやさ・きようさ）の日本語表示名を集約した定数群
    // バトル中のパラメータ名やバフ・デバフの対象名とは別物として扱う
    public static class PPUnitAbilityDefinition
    {
        // 最大 HP の元になる能力
        public const string NameHp = "HP";
        // 攻撃力の元になる能力
        public const string NameStrength = "ちから";
        // 防御力の元になる能力
        public const string NameGuard = "まもり";
        // 素早さの元になる能力
        public const string NameAgility = "はやさ";
        // 会心率の元になる能力
        public const string NameDexterity = "きようさ";

        // 成長曲線のラベルに付ける接尾辞
        public const string GrowthCurveSuffix = "成長曲線";
    }
}
