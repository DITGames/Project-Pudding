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
    // アイテム定義とパーティから UI 表示用の情報を供給する実装
    // 値を保持せず、参照するたびに定義とインベントリから読み直す
    // 所持数の変化はインベントリのイベントを中継して UI へ伝える
    // インベントリを購読するため、UI を破棄する際は必ず Dispose を呼ぶこと
    public class PPItemStatusSource : IPPItemStatusSource, IDisposable
    {
        // 表示対象のアイテム定義
        private readonly PPItemDefinition mDefinition;
        // 所持数とリソースを引くためのパーティ
        private readonly PPBattleParty mParty;
        // 購読解除済みかどうか。Dispose の多重呼び出しを無害にする
        private bool mIsDisposed;
        // 表示内容が変化したときに発火する
        public event Action Changed;

        // UI 表示名
        public string DisplayName => mDefinition.DisplayName;
        // 使用に必要なリソースコスト
        public PPResourceCost Cost => mDefinition.Cost;
        // 現在の所持数
        public int Count => mParty.Inventory.CountOf(mDefinition);

        // リソースが足りていて、かつ 1 つ以上所持している場合に使用可能
        public bool IsUsable =>
            mParty.ResourcePool.CanPay(mDefinition.Cost) && Count > 0;


        // aDefinition : 表示対象のアイテム定義
        // aParty : 所持数とリソースの参照元パーティ
        public PPItemStatusSource(PPItemDefinition aDefinition, PPBattleParty aParty)
        {
            mDefinition = aDefinition;
            mParty = aParty;
            mParty.Inventory.Changed += Raise;
        }

        // インベントリの変化を自身のイベントとして中継する
        private void Raise() => Changed?.Invoke();

        // インベントリへの購読を解除する。UI を閉じる際に必ず呼ぶこと。二度呼ばれても安全
        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;

            mParty.Inventory.Changed -= Raise;
        }
    }
}
