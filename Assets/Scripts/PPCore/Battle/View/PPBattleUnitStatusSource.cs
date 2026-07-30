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
    // BattleUnit を UI 向けの表示情報として見せるアダプタ
    // 値を保持せず参照のたびに読み直し、HP と最大 HP の変化を購読して Changed へ中継する
    // ユニットのパラメータを購読するため、UI を破棄する際は必ず Dispose を呼ぶこと
    // 呼ばないとユニットが生きている限りこのアダプタが解放されない
    public class PPBattleUnitStatusSource : IPPUnitStatusSource, IDisposable
    {
        // 表示対象のユニット
        private readonly BattleUnit mBattleUnit;
        // 購読解除済みかどうか。Dispose の多重呼び出しを無害にする
        private bool mIsDisposed;
        // 表示内容が変化したときに発火する
        public event Action Changed;

        // UI 表示名
        public string DisplayName => mBattleUnit.DisplayName;
        // 現在 HP
        public float CurrentHP => mBattleUnit.Parameters.Hp.CurrentValue;
        // 最大 HP。バフで変動しうるため現在値を返す
        public float MaxHP => mBattleUnit.Parameters.Hp.Max.CurrentValue;

        // aBattleUnit : 表示対象のユニット
        public PPBattleUnitStatusSource(BattleUnit aBattleUnit)
        {
            mBattleUnit = aBattleUnit;
            // 最大HPもバフで変わるため両方購読する
            mBattleUnit.Parameters.Hp.OnValueChanged += HandleChanged;
            mBattleUnit.Parameters.Hp.Max.OnValueChanged += HandleChanged;
        }

        // パラメータの変化を自身のイベントとして中継する
        // 解除できるようラムダではなく名前付きメソッドにしてある
        private void HandleChanged(IReadableParameter _) => Changed?.Invoke();

        // ユニットのパラメータへの購読を解除する。UI を破棄する際に呼ぶ
        // 二度呼ばれても安全
        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;

            mBattleUnit.Parameters.Hp.OnValueChanged -= HandleChanged;
            mBattleUnit.Parameters.Hp.Max.OnValueChanged -= HandleChanged;
        }
    }
}
