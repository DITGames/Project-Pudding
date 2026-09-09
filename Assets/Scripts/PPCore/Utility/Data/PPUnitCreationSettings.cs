/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitCreationSettings.cs
 * @author hqrse
 * @date 2026/09/09
 * @brief ユニット作成時の表示設定（ランタイムのレベル制限には使用しない）
 * =====================================*/

using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    public sealed class PPUnitCreationSettings : ScriptableObject
    {
        [Label("作成時の最大レベル")][SerializeField] private int mMaxLevel = 50;
        [Label("チャート基準値")][SerializeField] private float[] mScales;
        public int MaxLevel => mMaxLevel;
        public float[] Scales => mScales == null ? null : (float[])mScales.Clone();

        // 作成設定を記録するだけで、ユニットの評価範囲は変更しない
        public void Initialize(int aMaxLevel, float[] aScales)
        {
            mMaxLevel = aMaxLevel;
            mScales = (float[])aScales.Clone();
        }
    }
}
