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
    // ユニットの能力値を「育成値 → BaseValue → CurrentValue（BaseValue × バフ・デバフ）」の段階で扱うテーブル
    // 再計算のたびに育成値から組み立て直す
    public class PPBaseParameterTable
    {
        /* 内部型 */

        // パラメータ 1 つ分の登録内容
        protected class Entry
        {
            // BaseValue を書き込む先のパラメータ
            public Parameter Target;
            // 育成値
            public float GrowthValue;
        }

        /* 状態 */

        // パラメータ ID → 登録内容
        protected readonly Dictionary<string, Entry> mEntries = new();

        /* プロパティ */

        // 登録済みのパラメータ ID 一覧
        public IEnumerable<string> Ids => mEntries.Keys;

        /* 初期化・接続 */

        // パラメータを登録する。登録時点の BaseValue をそのまま育成値として扱う
        // ランタイムユニットの生成直後で呼ぶ前提
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

        /* 操作 */

        // 育成値を差し替えて BaseValue を組み立て直す。レベルアップなど恒久的な成長で使う
        public void SetGrowthValue(string aId, float aGrowthValue)
        {
            if (!mEntries.TryGetValue(aId, out var entry)) return;
            entry.GrowthValue = aGrowthValue;
            Recalculate(aId);
        }

        // 育成値から BaseValue を組み立て直し、CurrentValue も再計算する
        // 上限系（最大 HP）の場合、上限が下がれば ResourceParameter 側で残量が切り詰められる
        public virtual void Recalculate(string aId)
        {
            if (!mEntries.TryGetValue(aId, out var entry)) return;
            entry.Target.SetBaseValue(entry.GrowthValue);
        }

        // 登録済みのすべてのパラメータを組み立て直す
        public void RecalculateAll()
        {
            foreach (var id in mEntries.Keys)
            {
                Recalculate(id);
            }
        }

        /* 問い合わせ */

        // 指定 ID が登録済みかを返す
        public bool Contains(string aId) => mEntries.ContainsKey(aId);

        // 育成値を取得する
        public float GetGrowthValue(string aId)
            => mEntries.TryGetValue(aId, out var entry) ? entry.GrowthValue : 0f;
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
        /* 定数 */

        // 新規作成したユニット定義のきようさ既定値（会心率 5%）
        public const float DefaultDexterity = 20f;

        /* インスペクタ設定 */

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
