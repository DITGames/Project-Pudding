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
    
    public enum PPTypeAttribute
    {
        [InspectorName(PPTypeAttributeDefinition.TypeNormal)]
        Normal = 0,
        [InspectorName(PPTypeAttributeDefinition.TypeFire)]
        Fire = 1,
        [InspectorName(PPTypeAttributeDefinition.TypeWater)]
        Water = 2,
        [InspectorName(PPTypeAttributeDefinition.TypeEarth)]
        Earth = 3,
        [InspectorName(PPTypeAttributeDefinition.TypeShine)]
        Shine = 4,
        [InspectorName(PPTypeAttributeDefinition.TypeDark)]
        Dark = 5,
    }

    public static class PPTypeAttributeDefinition
    {
        public const int TypeCount = 6;
        public const int AttributeCount = 5;
        public const int BaseIndex = (int)PPTypeAttribute.Normal;
        
        public const string TypeNormal = "ノーマル";
        public const string TypeFire = "火";
        public const string TypeWater = "水";
        public const string TypeEarth = "土";
        public const string TypeShine = "光";
        public const string TypeDark = "闇";
        public static bool IsAttribute(PPTypeAttribute a) => a != PPTypeAttribute.Normal;
    }
}