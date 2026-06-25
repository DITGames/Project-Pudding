/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleUnitStatusSource.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief バトル側アダプタ
 * =====================================*/
using CommandBattleCore;
using System;

namespace PPBattle
{
    public class PPBattleUnitStatusSource : IPPUnitStatusSource
    {
        private readonly BattleUnit mBattleUnit;
        public event Action Changed;
        
        public string DisplayName => mBattleUnit.DisplayName;
        public float CurrentHP => mBattleUnit.Parameters.Hp.CurrentValue;
        public float MaxHP => mBattleUnit.Parameters.Hp.Max.CurrentValue;

        public PPBattleUnitStatusSource(BattleUnit aBattleUnit)
        {
            mBattleUnit = aBattleUnit;
            mBattleUnit.Parameters.Hp.OnValueChanged += _ => Changed?.Invoke();
            mBattleUnit.Parameters.Hp.Max.OnValueChanged += _ => Changed?.Invoke();
        }
    }
}