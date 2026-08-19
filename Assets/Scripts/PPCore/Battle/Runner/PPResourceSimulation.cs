/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPCoinResourceSimulation.cs
 * @author hqrse
 * @date 2026/08/03
 * @brief コイン取得シミュレーション
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    [Serializable]
    public struct PPCoinResourceSimEntry
    {
        [Label("タイプ")]
        public PPTypeAttribute mType;
        [Label("量")]
        public Vector2Int mAmount;
    }
    
    [Serializable]
    public class PPResourceSimulation
    {
        [Label("リソース", true)]
        public List<PPCoinResourceSimEntry> mEntries = new ();
    }
}