/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyAIProfileDefinition.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief 敵パーティAIの性格定義
 * =====================================*/
using System;
using CommandBattleCore;
using UnityEngine;

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
        [Label("メンバー数評価値")] public float MemberCountSocre = 3f;
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
        [Label("高コスト時スコア減少率")] public float HighCostDecreaseRate = 8.0f;
    }

    [Serializable]
    public sealed class PPWaitScore
    {
        [Label("基礎スコア")] public float BaseScore = 0.4f;
        [Label("温存バイアス")] public float SaveBias = 0.8f;
    }
    
    
    [CreateAssetMenu(fileName = "PPPartyAIProfileDefinition", menuName = "Project-Pudding/AI/PPPartyAIProfileDefinition")]
    public class PPPartyAIProfileDefinition : ScriptableObject
    {
        [Header("性格")]
        [Label("攻撃性 (0-1)")] public float Aggression = 0.5f;
        [Label("溜め傾向のバイアス (0-1)")] public float WaitBias = 0.4f;
        
        [Header("リソース")]
        [Label("温存するリソースの下限")] public float ReserveResources = 0f;
        [Label("スキル発動の閾値倍率")] public float SkillThreshold = 1.2f;
        
        [Header("行動")]
        [Label("1ティックあたりの最大同時行動数")] public int MaxActionsPerTick = 1;
        [Label("思考間隔(秒)")] public float ThinkInterval = 2f;
        [Label("ターゲット集中度 (0-1)")] public float FocusFire =  0.5f;
        
        [Header("ロール")]
        [Label("重み")] public PPRoleWeights Weights = new PPRoleWeights();
        [Label("行動順")] public PPRoleOrder Order = new PPRoleOrder();
        
        [Header("スコア")]
        [Label("攻撃スコア")] public PPAIAttackScore AttackScore = new PPAIAttackScore();
        [Label("スキルスコア")] public PPAISkillScore SkillScore  = new PPAISkillScore();
        [Label("回復スコア")] public PPAIHealScore HealScore  = new PPAIHealScore();
        [Label("サポートスコア")] public PPAISupportScore SupportScore  = new PPAISupportScore();
        [Label("コストスコア")] public PPCostScore CostScore  = new PPCostScore();
        [Label("ウェイトスコア")] public PPWaitScore WaitScore  = new PPWaitScore();
    }
}