/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleUnitStatusSource.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief バトルユニット情報読み取りアダプタ
 * =====================================*/
using CommandBattleCore;
using System;

namespace PPCore
{
    /// <summary>
    /// <see cref="BattleUnit"/> を UI 向けの表示情報として見せるアダプタ。
    /// 値を保持せず参照のたびに読み直し、HP と最大 HP の変化を購読して
    /// <see cref="Changed"/> へ中継する。
    /// </summary>
    /// <remarks>
    /// 購読をラムダで登録しているため解除する手段がない。
    /// ユニットより先にこのアダプタを捨てる使い方をすると参照が残る点に注意
    /// （他のソース実装は Dispose を持つ）。
    /// </remarks>
    public class PPBattleUnitStatusSource : IPPUnitStatusSource
    {
        /// <summary>表示対象のユニット。</summary>
        private readonly BattleUnit mBattleUnit;
        /// <summary>表示内容が変化したときに発火する。</summary>
        public event Action Changed;

        /// <summary>UI 表示名。</summary>
        public string DisplayName => mBattleUnit.DisplayName;
        /// <summary>現在 HP。</summary>
        public float CurrentHP => mBattleUnit.Parameters.Hp.CurrentValue;
        /// <summary>最大 HP。バフで変動しうるため現在値を返す。</summary>
        public float MaxHP => mBattleUnit.Parameters.Hp.Max.CurrentValue;

        /// <param name="aBattleUnit">表示対象のユニット。</param>
        public PPBattleUnitStatusSource(BattleUnit aBattleUnit)
        {
            mBattleUnit = aBattleUnit;
            // 最大HPもバフで変わるため両方購読する
            mBattleUnit.Parameters.Hp.OnValueChanged += _ => Changed?.Invoke();
            mBattleUnit.Parameters.Hp.Max.OnValueChanged += _ => Changed?.Invoke();
        }
    }
}
