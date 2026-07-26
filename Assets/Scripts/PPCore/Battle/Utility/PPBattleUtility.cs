/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleUtility.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief バトル汎用
 * =====================================*/

using Unity.VisualScripting;
using UnityEngine;

namespace PPCore
{
    public enum PPBattleRole
    {
        [InspectorName(PPBattleUtilityDefinition.RoleNameAttack)]
        Attacker,
        [InspectorName(PPBattleUtilityDefinition.RoleNameSupport)]
        Supporter,
        [InspectorName(PPBattleUtilityDefinition.RoleNameHeal)]
        Healer,
        [InspectorName("なし")]
        None,
    }
    
    public enum PPBattleSkillRole
    {
        [InspectorName(PPBattleUtilityDefinition.RoleNameAttack)]
        Attack,
        [InspectorName(PPBattleUtilityDefinition.RoleNameSupport)]
        Support,
        [InspectorName(PPBattleUtilityDefinition.RoleNameHeal)]
        Heal,
        [InspectorName("スペシャル")]
        Special,
    }

    public enum PPBattleActionRole
    {
        [InspectorName(PPBattleUtilityDefinition.RoleNameAttack)]
        Attack,
        [InspectorName(PPBattleUtilityDefinition.RoleNameSupport)]
        Support,
        [InspectorName(PPBattleUtilityDefinition.RoleNameHeal)]
        Heal,
        [InspectorName("なし")]
        None,
    }
    
    
    public static class PPBattleUtilityDefinition
    {
        public const string RoleNameAttack = "攻撃";
        public const string RoleNameSupport = "サポート";
        public const string RoleNameHeal = "回復";
    }
}