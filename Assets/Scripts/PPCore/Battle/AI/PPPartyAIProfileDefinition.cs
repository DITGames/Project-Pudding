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
    // パーティAIが割り当てるロール
    public enum PPBattleRole
    {
        Attacker,
        Supporter,
        Healer,
        None,
    }
    
    [Serializable]
    public sealed class PPRoleWeights
    {
        [Label("攻撃")] public float Attack = 1.0f;
        [Label("スキル")] public float Skill = 1.0f;
        [Label("支援")] public float Support = 1.0f;
        [Label("回復")] public float Heal = 1.0f;
    }

    [Serializable]
    public sealed class PPRoleOrder
    {
        [Label("攻撃")] public int Attack = 1;
        [Label("支援")] public int Support = 0;
        [Label("回復")] public int Heal = 2;
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
        [Label("リソース割合バイアス")] public float ResourceRatioBias = 0.15f;
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
        [Label("最低スコア")] public float MinScore = 0.3f;
        [Label("効率ベース")] public float Efficiency = 1.15f;
        [Label("最小単価")] public float MinUnitPrice = 0.35f;
        [Label("最大単価")] public float MaxUnitPrice = 1.0f;
    }

    [Serializable]
    public sealed class PPWaitScore
    {
        [Label("基礎スコア")] public float BaseScore = 0.4f;
        [Label("温存バイアス")] public float SaveBias = 0.8f;
    }

    [Serializable]
    public sealed class PPAISituationScore
    {
        [Label("攻撃倍率")] [Range(0f, 10f)] public float Attack = 1f;
        [Label("スキル倍率")] [Range(0f, 10f)] public float Skill = 1f;
        [Label("サポート倍率")] [Range(0f, 10f)] public float Support = 1f;
        [Label("回復倍率")] [Range(0f, 10f)] public float Heal = 1f;
        [Label("待機倍率")] [Range(0f, 10f)] public float Wait = 1f;
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
        [Header("性格")]
        [Label("攻撃性 (0-1)")] public float Aggression = 0.5f;
        [Label("溜め傾向のバイアス (0-1)")] public float WaitBias = 0.4f;
        
        [Header("リソース")]
        [Label("ベース温存量")] public float BaseReserve = 0f;
        [Label("ベースコスト重み")] public float BaseCostWeight = 1.1f;
        [Label("コスト感度")] public float CostSensitivity = 0.6f;
        [Label("オーバーフロー閾値")] public float OverflowThreshold = 0.85f;
        [Label("オーバーフロー重み")] public float OverflowWeight = 0.5f;
        [Label("スキル発動の閾値倍率")] public float SkillThreshold = 1.2f;
        
        [Header("行動")]
        [Label("1ティックあたりの最大同時行動数")] public int MaxActionsPerTick = 1;
        [Label("思考間隔(秒)")] public float ThinkInterval = 2f;
        [Label("ターゲット集中度 (0-1)")] public float FocusFire =  0.5f;
        
        [Header("ロール")]
        [Label("重み")] public PPRoleWeights Weights = new PPRoleWeights();
        [Label("行動順")] public PPRoleOrder Order = new PPRoleOrder();
        
        [Header("状況")]
        [Label("状況リスト (上のほうが評価優先度が高い)", true)]
        public List<PPPartyAISituationRule> Rules = new();
        [Label("デフォルト行動スコア")]
        public PPAISituationScore DefaultScore = new();
        
        
        [Header("スコア")]
        [Label("攻撃スコア")] public PPAIAttackScore AttackScore = new PPAIAttackScore();
        [Label("スキルスコア")] public PPAISkillScore SkillScore  = new PPAISkillScore();
        [Label("回復スコア")] public PPAIHealScore HealScore  = new PPAIHealScore();
        [Label("サポートスコア")] public PPAISupportScore SupportScore  = new PPAISupportScore();
        [Label("コストスコア")] public PPCostScore CostScore  = new PPCostScore();
        [Label("ウェイトスコア")] public PPWaitScore WaitScore  = new PPWaitScore();
    }
}