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
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.Serialization;

namespace PPCore
{
    [Serializable]
    public sealed class PPAISituationScore
    {
        [Label(PPBattleUtilityDefinition.RoleNameAttack)][Range(0, 10)]
        public float Attack = 1f;
        [Label(PPBattleUtilityDefinition.RoleNameSupport)][Range(0, 10)]
        public float Support = 1f;
        [Label(PPBattleUtilityDefinition.RoleNameHeal)][Range(0, 10)]
        public float Heal = 1f;
        
        [Header("プロファイル上書き")]
        [Label("積極性の乗算補正")][Range(0, 3)] public float AggressionMultiplier = 1f;
        [Label("忍耐係数の乗算補正")][Range(0, 3)] public float PatienceMultiplier = 1f;
    }
    
    [Serializable]
    public sealed class PPRoleWeights
    {
        [Label(PPBattleUtilityDefinition.RoleNameAttack)][Range(0, 10)]
        public float Attack = 1.0f;
        [Label(PPBattleUtilityDefinition.RoleNameSupport)][Range(0, 10)]
        public float Support = 1.0f;
        [Label(PPBattleUtilityDefinition.RoleNameHeal)][Range(0, 10)]
        public float Heal = 1.0f;
    }

    [Serializable]
    public sealed class PPRoleOrder
    {
        [Label(PPBattleUtilityDefinition.RoleNameAttack)]
        public int Attack = 1;
        [Label(PPBattleUtilityDefinition.RoleNameSupport)] public int Support = 0;
        [Label(PPBattleUtilityDefinition.RoleNameHeal)] public int Heal = 2;
        [Label("デフォルト")] public int Default = 3;
    }

    [Serializable]
    public sealed class PPAIAttackScore
    {
        [Label("基礎スコア")] public float BaseScore = 0.6f;
        [Label("HP割合バイアス")] public float HpRatioBias = 0.8f;
    }

    [Serializable]
    public sealed class PPAISkillScore
    {
        [Label("基礎スコア")] public float BaseScore = 0.9f; 
        [Label("範囲攻撃スコア")] public float RangeSkillScore = 0.4f; 
        [Label("HP割合バイアス")] public float HpRatioBias = 0.9f;
    }

    [Serializable]
    public sealed class PPAISupportScore
    {
        [Label("基礎スコア")] public float BaseScore = 0.4f;
        [Label("メンバー数評価値")] public float MemberCountScore = 3f;
        [Label("メンバー数バイアス")] public float MemberCountBias = 0.6f;
    }

    [Serializable]
    public sealed class PPAIHealScore
    {
        [Label("回復閾値")] public float Threshold = 0.1f;
        [Label("HP割合低下時バイアス")] public float HpRatioBias = 1.8f;
    }

    [Serializable]
    public sealed class PPCostScore
    {
        [Label("基準コスト")] public float ReferenceCost = 30f;
    }

    [Serializable]
    public sealed class PPPartyAISituationRule
    {
        [Label("ルール名")] public string Name = "New Situation";
        [Label("条件リスト", true)] public List<PPPartyConditionValidator> Conditions = new();
        [Label("成立時スコア")] public PPAISituationScore Score = new();
    }
    
    
    [CreateAssetMenu(fileName = "PPPartyAIProfileDefinition", menuName = "Project-Pudding/AI/PPPartyAIProfileDefinition")]
    public class PPPartyAIProfileDefinition : ScriptableObject
    {
        [Header("性能")]
        [Label("積極性")][Range(0,100)] public float Aggression = 50f;
        [Label("警戒度")][Range(0, 100)] public float Caution = 50f;
        
        [Header("リソース")]
        [Label("コスト感度")] public float CostSensitivity = 0.6f;
        [Label("リソース推移サンプル数")] public int TrendSampleCount = 4;
        
        [Header("行動")]
        [Label("思考間隔(秒)")] public float ThinkInterval = 0.5f;
        [Label("同時行動数上限")] public int MaxActionsPerTick = 3;
        [Label("ターゲット集中度")][Range(0, 1)] public float FocusFire = 0.5f;
        
        [Header("ロール")]
        [Label("重み")] public PPRoleWeights Weights = new();
        [Label("行動順")] public PPRoleOrder Order = new();
        
        [Header("スコア")]
        [Label("攻撃スコア")] public PPAIAttackScore AttackScore = new();
        [Label("スキルスコア")] public PPAISkillScore SkillScore = new();
        [Label("サポートスコア")] public PPAISupportScore SupportScore = new();
        [Label("回復スコア")] public PPAIHealScore HealScore = new();
        [Label("コストスコア")] public PPCostScore CostScore = new();
        
        [Header("シチュエーション")]
        [Label("デフォルトルール")] public PPAISituationScore DefaultScore = new();
        [Label("シチュエーション別ルール")] public List<PPPartyAISituationRule> Rules = new();
    }
}