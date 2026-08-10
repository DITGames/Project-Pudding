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

namespace PPCore
{
    // 状況が成立したときに適用される係数一式
    // ロール別の状況係数（登録が無いロールは 1 = 補正なし）に加えて、
    // 忍耐係数をこの状況だけ上書きできる
    // 旧 AI（PPPartyAIStrategistBase）専用。新 AI は PPAIDoctrine を使う
    [Serializable]
    public sealed class PPAISituationScore
    {
        // ロール別の状況係数。登録の無いロールは 1（補正なし）として扱われる
        [Label("ロール別係数", true)] public List<PPRoleValue> Roles = new();

        // この状況における忍耐係数の乗算補正。待機判定の許容ティック数に掛かる
        [Header("プロファイル上書き")]
        [Label("忍耐係数の乗算補正")][Range(0, 3)] public float PatienceMultiplier = 1f;
    }

    // ロール別の実行順序。同一ティックで複数行動が採用されたときの並び順を決める
    // 値が小さいほど先に実行される（既定では支援 → 攻撃 → 回復の順）
    [Serializable]
    public sealed class PPRoleOrder
    {
        [Label(PPBattleUtilityDefinition.RoleNameAttack)]
        public int Attack = 1;
        [Label(PPBattleUtilityDefinition.RoleNameSupport)] public int Support = 0;
        [Label(PPBattleUtilityDefinition.RoleNameHeal)] public int Heal = 2;
        // ロール未分類（Special 含む）の行動の実行順序
        [Label("デフォルト")] public int Default = 3;
    }

    // 状況ルール 1 件。条件をすべて満たしたときに作戦の差分が適用される
    // 条件は AND 判定。成立したルールは優先度の小さい順に重ねられ、後から重ねたものが勝つ
    [Serializable]
    public sealed class PPPartyAISituationRule
    {
        // ルール名。デバッグ表示に使われる
        [Label("ルール名")] public string Name = "New Situation";
        [SerializeReference]
        [Label("条件リスト", true)] public List<PPPartyConditionValidator> Conditions = new();

        // 適用順。小さいものから順に重ねるため、値が大きいルールほど最終的な指定が残る
        [Label("優先度")]
        [SerializeField] private int mPriority = 0;
        [Label("成立時の作戦差分")]
        [SerializeField] private PPAIDoctrineOverride mOverride = new();

        // 旧 AI（PPPartyAIStrategistBase）が参照する成立時スコア。新 AI は Override を使う
        [Header("旧AI用")]
        [Label("成立時スコア")] public PPAISituationScore Score = new();

        public int Priority => mPriority;
        public PPAIDoctrineOverride Override => mOverride;
    }


    // 敵パーティ AI の性格を定義する ScriptableObject
    // AI の挙動はほぼすべてこのアセットの値で決まる
    // スキル自体の強さは PPSkillDefinition 側のロール別 AI スコアで表現するため、
    // このアセットは「性格」（状況への反応・リソースの使い方の好み）だけを持つ
    // 強弱は行動のランダム性ではなく、思考機能の有効・無効で表現する。
    // 全機能を OFF にしたものがザコ相当（効用は計算するが、先を見越して取っておかない）になる
    [CreateAssetMenu(fileName = "PPPartyAIProfileDefinition", menuName = "Project-Pudding/AI/PPPartyAIProfileDefinition")]
    public class PPPartyAIProfileDefinition : ScriptableObject
    {
        [Label("説明")]
        [SerializeField][Multiline] protected string mDescription = "";
        
        // 有効にした思考だけが働く。ティアの差はここで表現する
        [Header("思考機能")]
        [Label("リソースを値付けする(λ)")]
        [SerializeField] private bool mIsUseLambda = false;
        [Label("保険を張る")]
        [SerializeField] private bool mIsUseInsurance = false;
        [Label("溜めを行う")]
        [SerializeField] private bool mIsUseBanking = false;

        // 値域はインスペクタでは制限せず、参照側（PPPartyBudgetPlanner）で丸める
        // 属性ドロワーは 1 フィールドにつき 1 つしか適用されないため、
        // 表示名（Label）と表示条件（EditCondition）を優先して Range は付けていない
        [Header("リソース感度")]
        [Label("λ基準倍率(0〜3)")]
        [EditCondition(nameof(mIsUseLambda), true)]
        [SerializeField] private float mLambdaScale = 1f;
        [Label("溢れを嫌う強さ(0〜3)")]
        [EditCondition(nameof(mIsUseLambda), true)]
        [SerializeField] private float mOverflowAversion = 1f;
        [Label("保険(回復スキル何回分)")]
        [EditCondition(nameof(mIsUseInsurance), true)]
        [SerializeField] private int mInsuranceCount = 1;

        // 警戒度（0〜1）。収入のばらつきをどれだけ厳しく見るかに使う。高いほど溜めに賭けない
        [Header("性能")]
        [PercentLabel("警戒度")] public float Caution = 0.5f;
        // 知能（0〜1）。高いほど最適な対象を選びやすくなる
        [PercentLabel("知能")] public float Intelligence = 0.5f;

        // 収入・リソース推移の平均を取るサンプル数
        [Header("リソース")]
        [Label("リソース推移サンプル数")] public int TrendSampleCount = 4;

        // 思考間隔（秒）。PPEnemyAIDriver の駆動周期
        [Header("行動")]
        [Label("思考間隔(秒)")] public float ThinkInterval = 0.5f;
        // 1 ティックで採用する行動数の上限
        [Label("同時行動数上限")] public int MaxActionsPerTick = 3;

        // 行動選択に乗せるノイズ。知能とは独立した軸で、大きいほど選択がぶれる
        [Label("行動選択ノイズ")]
        [Range(0f, 1f)]
        [SerializeField] private float mActionNoise = 0.2f;

        [Header("ロール")]
        [Label("行動順")] public PPRoleOrder Order = new();

        // どの状況ルールも成立しなかった場合に使われる既定の作戦
        [Header("シチュエーション")]
        [Label("デフォルト作戦")]
        [SerializeField] private PPAIDoctrine mDefaultDoctrine = new();
        // 状況別ルール。成立したものが優先度順に重ねられる
        [Label("シチュエーション別ルール")] public List<PPPartyAISituationRule> Rules = new();

        // 以下は旧 AI（PPPartyAIStrategistBase）専用の項目
        // 新 AI へ完全移行するまで比較検証できるよう残してある。新規アセットでは設定不要
        [Header("旧AI用")]
        [Label("重み", true)] public List<PPRoleValue> Weights = new();
        [Label("行動選択ノイズ最大比率")][Range(0, 1)] public float ActionNoiseAmplitude = 0.6f;
        [Label("デフォルトルール")] public PPAISituationScore DefaultScore = new();

        public bool IsUseLambda => mIsUseLambda;
        public bool IsUseInsurance => mIsUseInsurance;
        public bool IsUseBanking => mIsUseBanking;
        public float LambdaScale => mLambdaScale;
        public float OverflowAversion => mOverflowAversion;
        public int InsuranceCount => mInsuranceCount;
        public float ActionNoise => mActionNoise;
        public PPAIDoctrine DefaultDoctrine => mDefaultDoctrine;
    }
}
