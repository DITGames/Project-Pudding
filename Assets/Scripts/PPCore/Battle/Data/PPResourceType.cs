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
        Normal = 0,
        Fire = 1,
        Water = 2,
        Earth = 3,
        Shine = 4,
        Dark = 5,
    }

    public static class PPResource
    {
        public const int TypeCount = 6;
        public const int AttributeCount = 5;
        public const int BaseIndex = (int)PPResourceType.Normal;
        public static bool IsAttribute(PPResourceType aType) => aType != PPResourceType.Normal;
    }
}