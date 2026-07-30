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
    /// <summary>
    /// UI がユニットの表示情報を読み取るためのインターフェース。
    /// <para>
    /// View 側に <see cref="CommandBattleCore.BattleUnit"/> を直接触らせず、
    /// 表示に必要な項目だけを露出させるための境界。
    /// UI は <see cref="Changed"/> を購読して再描画すればよく、
    /// バトル側のどのイベントで値が変わるかを知る必要がない。
    /// </para>
    /// </summary>
    public interface IPPUnitStatusSource
    {
        /// <summary>UI 表示名。</summary>
        string DisplayName { get; }
        /// <summary>現在 HP。</summary>
        float CurrentHP { get; }
        /// <summary>最大 HP。</summary>
        float MaxHP { get; }

        /// <summary>表示内容が変化したときに発火する。</summary>
        event Action Changed;
    }
}
