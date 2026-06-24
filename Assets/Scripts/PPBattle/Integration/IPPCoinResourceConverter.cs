/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ICoinResourceConverter.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief 獲得したコインをリソースに変換する
 * =====================================*/
using UnityEngine;

namespace PPBattle
{
    public interface IPPCoinResourceConverter
    {
        float Convert(int aCoinCount);
    }

    public class PPLinearCoinResourceConverter : IPPCoinResourceConverter
    {
        public float Rate { get; set; } = 1f;

        public float Convert(int aCoinCount)
        {
            return aCoinCount * Rate;
        }
    }
}