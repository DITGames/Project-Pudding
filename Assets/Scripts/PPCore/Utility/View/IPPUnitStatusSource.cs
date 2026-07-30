/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPUnitStatusSource.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニット情報読み取りインターフェース
 * =====================================*/

using System;

namespace PPCore
{
    // UI がユニットの表示情報を読み取るためのインターフェース
    // View 側に CommandBattleCore.BattleUnit を直接触らせず、
    // 表示に必要な項目だけを露出させるための境界
    // UI は Changed を購読して再描画すればよく、バトル側のどのイベントで値が変わるかを知る必要がない
    public interface IPPUnitStatusSource
    {
        // UI 表示名
        string DisplayName { get; }
        // 現在 HP
        float CurrentHP { get; }
        // 最大 HP
        float MaxHP { get; }

        // 表示内容が変化したときに発火する
        event Action Changed;
    }
}
