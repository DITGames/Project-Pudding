/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleStatusSources.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief View へ表示データを供給するステータスソース（契約と実装）
 * =====================================*/

using System;

namespace PPCore
{
    // UI がユニットの表示情報を読み取るためのインターフェース
    // UI は Changed を購読して再描画する
    public interface IPPUnitStatusSource
    {
        /* 状態 */

        // UI 表示名
        string DisplayName { get; }
        // 現在 HP
        float CurrentHP { get; }
        // 最大 HP
        float MaxHP { get; }

        /* イベント */

        // 表示内容が変化したときに発火する
        event Action Changed;
    }

    // BattleUnit を UI 向けの表示情報として見せるアダプタ。HP の変化を購読して Changed へ中継する
    // UI を破棄する際は必ず Dispose を呼ぶこと
    public class PPBattleUnitStatusSource : IPPUnitStatusSource, IDisposable
    {
        /* 状態 */

        // 表示対象のユニット
        private readonly BattleUnit mBattleUnit;
        // 購読解除済みかどうか。Dispose の多重呼び出しを無害にする
        private bool mIsDisposed;

        /* プロパティ */

        // UI 表示名
        public string DisplayName => mBattleUnit.DisplayName;
        // 現在 HP
        public float CurrentHP => mBattleUnit.Parameters.Hp.CurrentValue;
        // 最大 HP。バフで変動しうるため現在値を返す
        public float MaxHP => mBattleUnit.Parameters.Hp.Max.CurrentValue;

        /* イベント */

        // 表示内容が変化したときに発火する
        public event Action Changed;

        /* 生成 */

        // aBattleUnit : 表示対象のユニット
        public PPBattleUnitStatusSource(BattleUnit aBattleUnit)
        {
            mBattleUnit = aBattleUnit;
            // 最大HPもバフで変わるため両方購読する
            mBattleUnit.Parameters.Hp.OnValueChanged += HandleChanged;
            mBattleUnit.Parameters.Hp.Max.OnValueChanged += HandleChanged;
        }

        /* 初期化・接続 */

        // ユニットのパラメータへの購読を解除する。UI を破棄する際に呼ぶ
        // 二度呼ばれても安全
        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;

            mBattleUnit.Parameters.Hp.OnValueChanged -= HandleChanged;
            mBattleUnit.Parameters.Hp.Max.OnValueChanged -= HandleChanged;
        }

        /* イベントハンドラ */

        // パラメータの変化を自身のイベントとして中継する
        // 解除できるようラムダではなく名前付きメソッドにしてある
        private void HandleChanged(IReadableParameter _) => Changed?.Invoke();
    }
}
