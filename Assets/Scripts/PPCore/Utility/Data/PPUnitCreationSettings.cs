/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitCreationSettings.cs
 * @author hqrse
 * @date 2026/09/09
 * @brief ユニット作成時の表示設定（ランタイムのレベル制限には使用しない）
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    public sealed class PPUnitCreationSettings : ScriptableObject
    {
        [Label("作成時の最大レベル")][SerializeField] private int mMaxLevel = 50;
        [Label("チャート基準値")][SerializeField] private float[] mScales;
        // 棒グラフの基準値の既定（HP・ちから・まもり・はやさ・きようさ）。表示専用で、超えても計算には影響しない
        public static readonly float[] DefaultScales = { 9999, 999, 999, 999, 999 };
        public int MaxLevel => mMaxLevel;
        public float[] Scales => mScales == null ? null : Complete(mScales);

        // 作成設定を記録するだけで、ユニットの評価範囲は変更しない
        public void Initialize(int aMaxLevel, float[] aScales)
        {
            mMaxLevel = aMaxLevel;
            mScales = (float[])aScales.Clone();
        }

        // きようさの基準値を持たない旧データ（4要素）は、不足分を既定値で補ったコピーを返す
        public static float[] Complete(float[] aScales)
        {
            var scales = new float[Mathf.Max(aScales.Length, DefaultScales.Length)];
            Array.Copy(DefaultScales, scales, DefaultScales.Length);
            Array.Copy(aScales, scales, aScales.Length);
            return scales;
        }
    }
}
