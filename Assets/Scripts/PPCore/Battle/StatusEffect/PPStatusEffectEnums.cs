/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStatusEffectEnums.cs
 * @author hqrse
 * @date 2026/07/31
 * @brief ステータスエフェクトの列挙（スタックポリシー・タグ・行動制限・分類）
 * =====================================*/

using System;
using UnityEngine;

namespace PPCore
{
    // 同一 ID のステータスエフェクトを重ねて付与したときの挙動
    // BattleUnit.AddStatusEffect がこの値を見て分岐する
    public enum StatusEffectStackPolicy
    {
        // 別で積む(複数同時処理が可能)
        Stack,
        // 継続時間のリフレッシュ
        Refresh,
        // スタック数を加算する。スタック数によって効果を変えるときなどに便利
        StackCount,
        // スタック数加算 + 継続時間リフレッシュ
        StackCountAndRefresh,
        // すでに付与されていた場合に新規付与をしない
        Ignore,
        // 既存付与を除去して置き換える
        Replace,
    }

    // 「毒」「火傷」といった具体名はゲーム側(Category)が持つ
    // ここにはどのゲームでも意味が変わらない分類だけを置く
    // 基底型を int にしているのは Unity のシリアライザが 64bit 幅の enum を扱えないため
    // long にすると [SerializeField] に載せた時点でシリアライズ対象から外れる
    // タグが 32 個を超える場合は、int のまま別 enum へ分けるか保持方法を見直すこと
    [Flags]
    public enum StatusEffectTag : int
    {
        None = 0,
        // 有利な効果
        Buff = 1 << 0,
        // 不利な効果
        Debuff = 1 << 1,
        // 状態異常
        Ailment = 1 << 2,
        // パラメータ変動を伴う
        ParameterMod = 1 << 3,
        // 継続的な増減を伴う
        Periodic = 1 << 4,
        // 解除スキルで消せない
        Unremovable = 1 << 5,
    }

    // ステータスエフェクトが課す行動制限の種類
    // 1 体に複数のエフェクトが同時に掛かるため、ビットフラグとして合成できるようにしてある
    // BattleUnit.CurrentRestrictions が全エフェクト分を OR で合成した現在値を返す
    [Flags]
    public enum ActionRestriction
    {
        // 制限なし
        None = 0,
        // 行動不可。麻痺、睡眠など
        [InspectorName("行動不可")]
        CannotAct = 1 << 0,
        // 行動のランダム化。混乱、魅了など
        [InspectorName("行動のランダム化")]
        Confused = 1 << 1,
        // スキル使用不可。沈黙など
        [InspectorName("スキル使用不可")]
        Silenced = 1 << 2,
        // 逃走不可
        [InspectorName("逃走不可")]
        CannotEscape = 1 << 3,
        // メンバー入れ替え不可
        [InspectorName("入れ替え不可")]
        CannotSwap = 1 << 4,
    }

    // bit 0-15 : 状態異常系
    // bit 16-31: パラメータ変動系
    // Unity のシリアライズが long 裏付けの enum に対応していないため int を使う（[SerializeField] で編集可能にするため）
    [Flags]
    public enum PPEffectCategory
    {
        None = 0,

        /* ---- 状態異常系 ---- */
        Poison = 1 << 0,
        Burn = 1 << 1,
        Paralyze = 1 << 2,
        // 脆弱：被ダメージ増加
        Vulnerable = 1 << 3,
        // 混乱：行動のランダム化
        Confused = 1 << 4,
        // 封印：スキル使用不可
        Silenced = 1 << 5,

        /* ---- パラメータ変動系 ---- */
        AttackBuff = 1 << 16,
        AttackDebuff = 1 << 17,
        DefenseBuff = 1 << 18,
        DefenseDebuff = 1 << 19,
        SpeedBuff = 1 << 20,
        SpeedDebuff = 1 << 21,
        MaxHpBuff = 1 << 22,
        MaxHpDebuff = 1 << 23,
        CostBuff = 1 << 24,
        CostDebuff = 1 << 25,
        ActionCountBuff = 1 << 26,
        ActionCountDebuff = 1 << 27,
        // きようさ（会心率の元）の増減。会心率アップ・ダウンはこの分類を使う
        DexterityBuff = 1 << 28,
        DexterityDebuff = 1 << 29,

        /* ---- まとめ(解除スキルのマスクとしてそのまま使う) ---- */
        AllAilment = Poison | Burn | Paralyze | Vulnerable | Confused | Silenced,
        AllBuff = AttackBuff | DefenseBuff | SpeedBuff | MaxHpBuff | CostBuff | ActionCountBuff | DexterityBuff,
        AllDebuff = AttackDebuff | DefenseDebuff | SpeedDebuff | MaxHpDebuff | CostDebuff | ActionCountDebuff | DexterityDebuff,
    }

    // パラメータ種別の日本語表示名を集約した定数群
    // PPModifierTargetParam の InspectorName 表示から参照される
    public static class PPParameterEffectCategoryDefinition
    {
        public const string NameAttack = "攻撃";
        public const string NameDefense = "防御";
        public const string NameSpeed = "素早さ";
        public const string NameHp = "HP";
        public const string NameCost = "コスト";
        public const string NameActionCount = "行動回数上限";
        public const string NameDexterity = "きようさ";
    }
}
