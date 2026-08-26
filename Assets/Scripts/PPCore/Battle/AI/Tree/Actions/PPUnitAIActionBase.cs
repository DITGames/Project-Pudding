/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIActionBase.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニットAIが実行する行動の基底クラス
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ユニット AI が実行する行動の基底クラス
    // 行動ノード（PPUnitAIActionNode）が 1 つ保持し、実行できる場合のみ確定させる
    // 実行できるかどうかの判定と、コマンドの組み立てを 1 つのメソッドで行うのは、
    // 「撃てるか調べてから作る」の二度手間を避け、判定と生成のズレを防ぐため
    // 派生クラスを追加するときは PPTypeMenuName を必ず付けること（型選択ピッカーがこれに依存する）
    [Serializable]
    public abstract class PPUnitAIActionBase
    {
        [Header("表示")]
        [Label("行動名")]
        [SerializeField] protected string mActionName = "";

        // 記録・ログに出す行動名。未入力なら型ごとの既定名を使う
        public string ActionName => string.IsNullOrEmpty(mActionName) ? DefaultActionName : mActionName;

        // 型ごとの既定表示名。派生側で上書きする
        protected abstract string DefaultActionName { get; }

        // 今の状況でこの行動を組み立てる
        // 派生クラスで実装する。バトルの状態を変えず、コマンドを作るだけに留めること
        // aContext : 評価 1 回分の入力
        // return : 組み立てられた行動。実行できない場合は PPUnitAINodeResult.Failed
        public abstract PPUnitAINodeResult Build(PPUnitAIEvalContext aContext);
    }
}
