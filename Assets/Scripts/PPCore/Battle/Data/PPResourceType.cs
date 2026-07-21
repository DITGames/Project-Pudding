/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPResourceType.cs
 * @author hqrse
 * @date 2026/07/18
 * @brief リソースのタイプ
 * =====================================*/
using UnityEngine;

namespace PPCore
{
    public enum PPResourceType
    {
        [InspectorName(PPResource.TypeNormal)]
        Normal = 0,
        [InspectorName(PPResource.TypeFire)]
        Fire = 1,
        [InspectorName(PPResource.TypeWater)]
        Water = 2,
        [InspectorName(PPResource.TypeEarth)]
        Earth = 3,
        [InspectorName(PPResource.TypeShine)]
        Shine = 4,
        [InspectorName(PPResource.TypeDark)]
        Dark = 5,
    }

    public static class PPResource
    {
        public const int TypeCount = 6;
        public const int AttributeCount = 5;
        public const int BaseIndex = (int)PPResourceType.Normal;
        
        public const string TypeNormal = "ベース";
        public const string TypeFire = "火";
        public const string TypeWater = "水";
        public const string TypeEarth = "土";
        public const string TypeShine = "光";
        public const string TypeDark = "闇";
        public static bool IsAttribute(PPResourceType aType) => aType != PPResourceType.Normal;
    }
}