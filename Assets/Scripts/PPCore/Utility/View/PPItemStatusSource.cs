/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPItemStatusSource.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief アイテム状態ソースベース
 * =====================================*/

using System;
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// アイテム定義とパーティから UI 表示用の情報を供給する実装。
    /// <para>
    /// 値を保持せず、参照するたびに定義とインベントリから読み直す。
    /// 所持数の変化はインベントリのイベントを中継して UI へ伝える。
    /// </para>
    /// <para>
    /// インベントリを購読するため、UI を破棄する際は必ず <see cref="Dispose"/> を呼ぶこと。
    /// </para>
    /// </summary>
    public class PPItemStatusSource : IPPItemStatusSource, IDisposable
    {
        /// <summary>表示対象のアイテム定義。</summary>
        private readonly PPItemDefinition mDefinition;
        /// <summary>所持数とリソースを引くためのパーティ。</summary>
        private readonly PPBattleParty mParty;
        /// <summary>購読解除済みかどうか。<see cref="Dispose"/> の多重呼び出しを無害にする。</summary>
        private bool mIsDisposed;
        /// <summary>表示内容が変化したときに発火する。</summary>
        public event Action Changed;

        /// <summary>UI 表示名。</summary>
        public string DisplayName => mDefinition.DisplayName;
        /// <summary>使用に必要なリソースコスト。</summary>
        public PPResourceCost Cost => mDefinition.Cost;
        /// <summary>現在の所持数。</summary>
        public int Count => mParty.Inventory.CountOf(mDefinition);

        /// <summary>リソースが足りていて、かつ 1 つ以上所持している場合に使用可能。</summary>
        public bool IsUsable =>
            mParty.ResourcePool.CanPay(mDefinition.Cost) && Count > 0;


        /// <param name="aDefinition">表示対象のアイテム定義。</param>
        /// <param name="aParty">所持数とリソースの参照元パーティ。</param>
        public PPItemStatusSource(PPItemDefinition aDefinition, PPBattleParty aParty)
        {
            mDefinition = aDefinition;
            mParty = aParty;
            mParty.Inventory.Changed += Raise;
        }

        /// <summary>インベントリの変化を自身のイベントとして中継する。</summary>
        private void Raise() => Changed?.Invoke();

        /// <summary>インベントリへの購読を解除する。UI を閉じる際に必ず呼ぶこと。二度呼ばれても安全。</summary>
        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;

            mParty.Inventory.Changed -= Raise;
        }
    }
}
