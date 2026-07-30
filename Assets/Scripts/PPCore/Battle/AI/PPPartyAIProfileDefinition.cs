/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyAIProfileDefinition.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief 敵パーティAIの性格定義
 * =====================================*/
using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.Serialization;

namespace PPCore
{
    /// <summary>
    /// 状況が成立したときに適用されるスコア一式。
    /// ロール別の重み付けに掛かるほか、積極性・忍耐係数をこの状況だけ上書きできる。
    /// 「ピンチなので回復優先かつ慎重に」といった振る舞いはここで表現する。
    /// </summary>
    [Serializable]
    public sealed class PPAISituationScore
    {
        /// <summary>攻撃行動のスコア倍率。</summary>
        [Label(PPBattleUtilityDefinition.RoleNameAttack)][Range(0, 10)]
        public float Attack = 1f;
        /// <summary>支援行動のスコア倍率。</summary>
        [Label(PPBattleUtilityDefinition.RoleNameSupport)][Range(0, 10)]
        public float Support = 1f;
        /// <summary>回復行動のスコア倍率。</summary>
        [Label(PPBattleUtilityDefinition.RoleNameHeal)][Range(0, 10)]
        public float Heal = 1f;

        /// <summary>この状況における積極性の乗算補正。攻撃系スコアに掛かる。</summary>
        [Header("プロファイル上書き")]
        [Label("積極性の乗算補正")][Range(0, 3)] public float AggressionMultiplier = 1f;
        /// <summary>この状況における忍耐係数の乗算補正。待機判定の許容ティック数に掛かる。</summary>
        [Label("忍耐係数の乗算補正")][Range(0, 3)] public float PatienceMultiplier = 1f;
    }

    /// <summary>
    /// ロール別のスコア重み。状況スコアとは別に、AI の基本的な行動傾向を決める。
    /// </summary>
    [Serializable]
    public sealed class PPRoleWeights
    {
        /// <summary>攻撃行動の重み。</summary>
        [Label(PPBattleUtilityDefinition.RoleNameAttack)][Range(0, 10)]
        public float Attack = 1.0f;
        /// <summary>支援行動の重み。</summary>
        [Label(PPBattleUtilityDefinition.RoleNameSupport)][Range(0, 10)]
        public float Support = 1.0f;
        /// <summary>回復行動の重み。</summary>
        [Label(PPBattleUtilityDefinition.RoleNameHeal)][Range(0, 10)]
        public float Heal = 1.0f;
    }

    /// <summary>
    /// ロール別の実行順序。同一ティックで複数行動が採用されたときの並び順を決める。
    /// 値が小さいほど先に実行される（既定では支援 → 攻撃 → 回復の順）。
    /// </summary>
    [Serializable]
    public sealed class PPRoleOrder
    {
        /// <summary>攻撃行動の実行順序。</summary>
        [Label(PPBattleUtilityDefinition.RoleNameAttack)]
        public int Attack = 1;
        /// <summary>支援行動の実行順序。</summary>
        [Label(PPBattleUtilityDefinition.RoleNameSupport)] public int Support = 0;
        /// <summary>回復行動の実行順序。</summary>
        [Label(PPBattleUtilityDefinition.RoleNameHeal)] public int Heal = 2;
        /// <summary>ロール未分類の行動の実行順序。</summary>
        [Label("デフォルト")] public int Default = 3;
    }

    /// <summary>通常攻撃のスコア計算に使う係数。</summary>
    [Serializable]
    public sealed class PPAIAttackScore
    {
        /// <summary>基礎スコア。対象の状態によらず常に乗る分。</summary>
        [Label("基礎スコア")] public float BaseScore = 0.6f;
        /// <summary>対象の HP が減っているほどスコアを押し上げる度合い。</summary>
        [Label("HP割合バイアス")] public float HpRatioBias = 0.8f;
    }

    /// <summary>攻撃スキルのスコア計算に使う係数。</summary>
    [Serializable]
    public sealed class PPAISkillScore
    {
        /// <summary>基礎スコア。</summary>
        [Label("基礎スコア")] public float BaseScore = 0.9f;
        /// <summary>対象を持たない範囲スキルの評価値。単体スキルの「とどめやすさ」の代わりに使われる。</summary>
        [Label("範囲攻撃スコア")] public float RangeSkillScore = 0.4f;
        /// <summary>対象の HP が減っているほどスコアを押し上げる度合い。</summary>
        [Label("HP割合バイアス")] public float HpRatioBias = 0.9f;
    }

    /// <summary>支援スキルのスコア計算に使う係数。</summary>
    [Serializable]
    public sealed class PPAISupportScore
    {
        /// <summary>基礎スコア。</summary>
        [Label("基礎スコア")] public float BaseScore = 0.4f;
        /// <summary>生存人数の評価基準。この人数で効果が最大とみなす。</summary>
        [Label("メンバー数評価値")] public float MemberCountScore = 3f;
        /// <summary>生存人数が多いほどスコアを押し上げる度合い。</summary>
        [Label("メンバー数バイアス")] public float MemberCountBias = 0.6f;
    }

    /// <summary>回復スキルのスコア計算に使う係数。</summary>
    [Serializable]
    public sealed class PPAIHealScore
    {
        /// <summary>回復を検討し始める緊急度の下限。これ未満ならスコア 0 になる。</summary>
        [Label("回復閾値")] public float Threshold = 0.1f;
        /// <summary>HP 低下が深刻なほどスコアを押し上げる度合い。</summary>
        [Label("HP割合低下時バイアス")] public float HpRatioBias = 1.8f;
    }

    /// <summary>コスト効率の計算に使う係数。</summary>
    [Serializable]
    public sealed class PPCostScore
    {
        /// <summary>コスト効率の基準となるコスト量。これを超えるほどスコアが減衰する。</summary>
        [Label("基準コスト")] public float ReferenceCost = 30f;
    }

    /// <summary>
    /// 状況ルール 1 件。条件をすべて満たしたときにスコアが適用される。
    /// 条件は AND 判定で、プロファイル内で後に定義されたルールほど優先される。
    /// </summary>
    [Serializable]
    public sealed class PPPartyAISituationRule
    {
        /// <summary>ルール名。デバッグ表示に使われる。</summary>
        [Label("ルール名")] public string Name = "New Situation";
        /// <summary>成立条件。すべて満たした場合のみルールが適用される。</summary>
        [Label("条件リスト", true)] public List<PPPartyConditionValidator> Conditions = new();
        /// <summary>成立時に適用されるスコア。</summary>
        [Label("成立時スコア")] public PPAISituationScore Score = new();
    }


    /// <summary>
    /// 敵パーティ AI の性格を定義する ScriptableObject。
    /// <para>
    /// <see cref="PPPartyAIStrategistBase"/> の挙動はほぼすべてこのアセットの値で決まる。
    /// AI の調整はコードではなくこのアセット（<c>Assets/GameData/AI/PartyAIProfile/</c>）で行う。
    /// </para>
    /// <para>
    /// 大きく「性能（積極性・警戒度・知能）」「リソース感度」「行動制御」
    /// 「ロール別の重みと順序」「行動種別ごとのスコア係数」「状況別ルール」に分かれる。
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "PPPartyAIProfileDefinition", menuName = "Project-Pudding/AI/PPPartyAIProfileDefinition")]
    public class PPPartyAIProfileDefinition : ScriptableObject
    {
        /// <summary>積極性（0〜1）。高いほど攻撃系のスコアが上がる。</summary>
        [Header("性能")]
        [PercentLabel("積極性")] public float Aggression = 0.5f;
        /// <summary>警戒度（0〜1）。高いほど「待って溜める」判断をしなくなる。</summary>
        [PercentLabel("警戒度")] public float Caution = 0.5f;
        /// <summary>知能（0〜1）。低いほど選択にノイズが乗り、最適解を外しやすくなる。</summary>
        [PercentLabel("知能")] public float Intelligence = 0.5f;

        /// <summary>コスト感度。高いほど高コストの行動を避ける。</summary>
        [Header("リソース")]
        [Label("コスト感度")] public float CostSensitivity = 0.6f;
        /// <summary>リソース増加トレンドの平均を取るサンプル数。</summary>
        [Label("リソース推移サンプル数")] public int TrendSampleCount = 4;

        /// <summary>思考間隔（秒）。<see cref="PPEnemyAIDriver"/> の駆動周期。</summary>
        [Header("行動")]
        [Label("思考間隔(秒)")] public float ThinkInterval = 0.5f;
        /// <summary>1 ティックで採用する行動数の上限。</summary>
        [Label("同時行動数上限")] public int MaxActionsPerTick = 3;
        /// <summary>行動選択に乗せるノイズの最大比率。知能が低いほどこの幅いっぱいまでぶれる。</summary>
        [Label("行動選択ノイズ最大比率")][Range(0, 1)] public float ActionNoiseAmplitude = 0.6f;

        /// <summary>ロール別のスコア重み。</summary>
        [Header("ロール")]
        [Label("重み")] public PPRoleWeights Weights = new();
        /// <summary>ロール別の実行順序。</summary>
        [Label("行動順")] public PPRoleOrder Order = new();

        /// <summary>通常攻撃のスコア係数。</summary>
        [Header("スコア")]
        [Label("攻撃スコア")] public PPAIAttackScore AttackScore = new();
        /// <summary>攻撃スキルのスコア係数。</summary>
        [Label("スキルスコア")] public PPAISkillScore SkillScore = new();
        /// <summary>支援スキルのスコア係数。</summary>
        [Label("サポートスコア")] public PPAISupportScore SupportScore = new();
        /// <summary>回復スキルのスコア係数。</summary>
        [Label("回復スコア")] public PPAIHealScore HealScore = new();
        /// <summary>コスト効率の係数。</summary>
        [Label("コストスコア")] public PPCostScore CostScore = new();

        /// <summary>どの状況ルールも成立しなかった場合に使われる既定スコア。</summary>
        [Header("シチュエーション")]
        [Label("デフォルトルール")] public PPAISituationScore DefaultScore = new();
        /// <summary>状況別ルール。上から順に評価され、成立したもので上書きされる（後勝ち）。</summary>
        [Label("シチュエーション別ルール")] public List<PPPartyAISituationRule> Rules = new();
    }
}
