/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitConditionValidator.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief ユニットAIが使うユニット条件の基底クラス
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ユニット 1 体が今その行動を取るべきかを判定する条件の基底。[SerializeReference] の通常クラスでツリーの条件リストに直接保持する
    // 派生クラスには PPTypeMenuName を必ず付ける（型選択ピッカーが依存する）
    [Serializable]
    public abstract class PPUnitConditionValidator
    {
        /* インスペクタ設定 */

        [Header("表示")]
        // 説明文を設定内容から自動生成するか。外すと書いた文面がそのまま残る
        // 枝を置いた意図など、自動生成では表せない説明を残したい場合に外す
        [Label("説明を自動生成する")]
        [SerializeField] protected bool mIsAutoDescription = true;

        [Label("説明")]
        [TextArea]
        [SerializeField] protected string mDescription;

        /* プロパティ */

        // 条件の説明文
        public string Description => mDescription;

        /* 評価・判定 */

        // 指定ユニットが条件を満たすか判定する
        // 派生クラスで実装する。状態を変えず判定のみを行うこと
        public abstract bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot);

        // 条件リストを AND で評価する
        // 空リストは「条件なし」とみなして true を返す。null 要素は読み飛ばす
        public static bool EvaluateAll(IReadOnlyList<PPUnitConditionValidator> aConditions,
            PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aConditions == null) return true;

            foreach (var condition in aConditions)
            {
                if (condition == null) continue;
                if (!condition.Evaluate(aUnit, aSnapShot)) return false;
            }
            return true;
        }

        /* 問い合わせ */

        // 条件リストが 1 件でも中身を持っているか
        // 「達成済み判定条件が空なら常に未達成」の判定に使う
        public static bool HasAny(IReadOnlyList<PPUnitConditionValidator> aConditions)
        {
            if (aConditions == null) return false;

            foreach (var condition in aConditions)
            {
                if (condition != null) return true;
            }
            return false;
        }

        /* 説明文 */

        // 設定内容から説明文を組み立て直す。ツリー編集のたびに呼び、サマリ表示と設定を一致させる
        // 自動生成を外している場合は何もしない
        public void RefreshDescription()
        {
            if (!mIsAutoDescription) return;

            BuildDescription();
        }

        // 比較演算子を説明文用の日本語へ変換する
        protected virtual string GetOpString(PPCompareOp aOp)
            => aOp switch
            {
                PPCompareOp.Equal => "等しい",
                PPCompareOp.NotEqual => "等しくない",
                PPCompareOp.GreaterOrEqual => "以上",
                PPCompareOp.LessOrEqual => "以下",
                PPCompareOp.GreaterThan => "より多い",
                PPCompareOp.LessThan => "未満",
                _ => ""
            };

        // 設定内容から mDescription を組み立てる
        // 派生クラスでオーバーライドして、インスペクタ上で条件の意味が読めるようにする
        protected virtual void BuildDescription()
        {
        }
    }
}
