/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPItemCommand.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief アイテムコマンドの拡張版
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 所持数とリソースを消費するアイテム使用コマンド
    // 基底の ItemCommand に対して、実行前にリソース残量と在庫を確認し、
    // 在庫を 1 つ減らしてから効果を発動する
    // リソースは CanPay で確認するだけで実際には消費していない
    // 意図的に「所持数だけを消費する」設計なのか、消費処理の実装漏れなのかは要確認
    public class PPItemCommand : ItemCommand
    {
        // 使用するアイテムの定義。コストと在庫の照合に使う
        private readonly PPItemDefinition mDefinition;

        // aUnit : アイテムを使用するユニット
        // aDefinition : 使用するアイテムの定義。効果本体も兼ねる
        // aResolver : 対象を決めるリゾルバ
        public PPItemCommand(BattleUnit aUnit, PPItemDefinition aDefinition, ITargetResolver aResolver)
            : base(aUnit, aDefinition, aResolver) => mDefinition = aDefinition;

        // パーティ種別・リソース残量・在庫を順に確認し、すべて通った場合のみ
        // 在庫を消費して基底の効果発動へ進む
        // aContext : 実行時のバトルコンテキスト
        public override void Execute(BattleContext aContext)
        {
            if (aContext.GetParty(Source.Side) is not PPBattleParty party) return;
            if (!party.ResourcePool.CanPay(mDefinition.Cost)) return;
            if (!party.Inventory.TryConsume(mDefinition))
            {
                Debug.Log("アイテムが足りません");
                return;
            }
            base.Execute(aContext);
        }
    }
}
