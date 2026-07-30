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
    /// <para>
    /// ユニットのパラメータを購読するため、UI を破棄する際は必ず <see cref="Dispose"/> を呼ぶこと。
    /// 呼ばないとユニットが生きている限りこのアダプタが解放されない。
    /// </para>
    /// </summary>
    public class PPBattleUnitStatusSource : IPPUnitStatusSource, IDisposable
    {
        /// <summary>表示対象のユニット。</summary>
        private readonly BattleUnit mBattleUnit;
        /// <summary>購読解除済みかどうか。<see cref="Dispose"/> の多重呼び出しを無害にする。</summary>
        private bool mIsDisposed;
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
            mBattleUnit.Parameters.Hp.OnValueChanged += HandleChanged;
            mBattleUnit.Parameters.Hp.Max.OnValueChanged += HandleChanged;
        }

        /// <summary>
        /// パラメータの変化を自身のイベントとして中継する。
        /// 解除できるようラムダではなく名前付きメソッドにしてある。
        /// </summary>
        private void HandleChanged(IReadableParameter _) => Changed?.Invoke();

        /// <summary>
        /// ユニットのパラメータへの購読を解除する。UI を破棄する際に呼ぶ。
        /// 二度呼ばれても安全。
        /// </summary>
        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;

            mBattleUnit.Parameters.Hp.OnValueChanged -= HandleChanged;
            mBattleUnit.Parameters.Hp.Max.OnValueChanged -= HandleChanged;
        }
    }
}
