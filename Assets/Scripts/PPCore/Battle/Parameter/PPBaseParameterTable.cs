/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBaseParameterTable.cs
 * @author hqrse
 * @date 2026/09/26
 * @brief 育成値とスキル効果値から BaseValue を組み立てるテーブル
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    // ユニットの能力値を「育成値 → BaseValue → CurrentValue」の 3 段階で扱うためのテーブル
    //   育成値       : レベル成長を反映した値。ここで別に保持し、スキル効果では書き換えない
    //   BaseValue    : 育成値 × S。S = max(0, 1 + そのパラメータに掛かるスキル効果値の合計)
    //   CurrentValue : BaseValue × B。B（バフ・デバフの割合加算）は Parameter 側の修飾子計算に任せる
    // 再計算のたびに育成値から組み立て直すため、スキル効果が二重に掛かることはない
    // 効果値は割合で持つ（+20% は 0.2、-10% は -0.1）。同じパラメータへの効果値は合計してから倍率にする
    // パッシブスキル・パーティ効果の仕組みはまだ無いため、ここでは集計と再計算の入口だけを用意している
    // （効果の発動条件・タイミングは未定。付与する側は AddSkillEffect / RemoveSkillEffectsFromSource を呼ぶだけでよい）
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
        // aId : パラメータ ID
        // aTarget : BaseValue を書き込む先のパラメータ
        public void Register(string aId, Parameter aTarget)
        {
            if (aTarget == null) return;
            Register(aId, aTarget, aTarget.BaseValue);
        }

        // 育成値を明示してパラメータを登録し、BaseValue を組み立て直す
        // aId : パラメータ ID
        // aTarget : BaseValue を書き込む先のパラメータ
        // aGrowthValue : 育成値
        public void Register(string aId, Parameter aTarget, float aGrowthValue)
        {
            if (aTarget == null) return;
            mEntries[aId] = new Entry { Target = aTarget, GrowthValue = aGrowthValue };
            Recalculate(aId);
        }

        // 指定 ID が登録済みかを返す
        public bool Contains(string aId) => mEntries.ContainsKey(aId);

        // 育成値を取得する
        // aId : パラメータ ID
        // return : 育成値。未登録なら 0
        public float GetGrowthValue(string aId)
            => mEntries.TryGetValue(aId, out var entry) ? entry.GrowthValue : 0f;

        // 育成値を差し替えて BaseValue を組み立て直す。レベルアップなど恒久的な成長で使う
        // aId : パラメータ ID
        // aGrowthValue : 新しい育成値
        public void SetGrowthValue(string aId, float aGrowthValue)
        {
            if (!mEntries.TryGetValue(aId, out var entry)) return;
            entry.GrowthValue = aGrowthValue;
            Recalculate(aId);
        }

        // スキル効果値を追加して BaseValue を組み立て直す
        // aId : 対象のパラメータ ID
        // aSource : 付与元。解除時の照合に使うため参照比較できるものを渡す
        // aValue : 効果値（割合。+20% は 0.2）
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
        // aId : パラメータ ID
        // return : 効果値の合計。未登録・効果なしなら 0
        public float GetSkillEffectSum(string aId)
        {
            if (!mEntries.TryGetValue(aId, out var entry)) return 0f;
            float sum = 0f;
            foreach (var effect in entry.SkillEffects) sum += effect.Value;
            return sum;
        }

        // スキル効果の倍率 S = max(0, 1 + 効果値の合計)
        // aId : パラメータ ID
        // return : 0 以上の倍率。効果なしなら 1
        public float GetSkillMultiplier(string aId)
            => Parameter.RatioSumToMultiplier(GetSkillEffectSum(aId));

        // 育成値とスキル効果値から BaseValue を組み立て直し、CurrentValue も再計算する
        // 上限系（最大 HP・スキルゲージ上限）の場合、上限が下がれば ResourceParameter 側で残量が切り詰められる
        // aId : パラメータ ID
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
}
