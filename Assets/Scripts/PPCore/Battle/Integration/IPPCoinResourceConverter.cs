/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ICoinResourceConverter.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief PPコインのリソース化コンバーター
 * =====================================*/
using UnityEngine;

namespace PPCore
{
    public interface IPPCoinResourceConverter
    {
        float Convert(int aCoinCount, float aRate);
    }

    public class PPLinearCoinResourceConverter : IPPCoinResourceConverter
    {
        public float Convert(int aCoinCount, float aRate) => aCoinCount * aRate;
    }
}