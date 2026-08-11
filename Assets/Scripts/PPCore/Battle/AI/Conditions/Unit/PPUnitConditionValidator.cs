/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitConditionValidator.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 戦術ステップが使うユニット条件の基底クラス
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 戦術ステップが「誰が実行するか」「そのステップは達成済みか」を判定するための条件の基底クラス
    // パーティ全体を見る PPPartyConditionValidator に対して、こちらはユニット 1 体を見る
    // 「指定タグのスキルを持っている」「攻撃力が高い」といった判定を 1 つずつクラス化し、
    // ステップ側が複数を AND で束ねて実行者を絞り込む
    // PPPartyConditionValidator と同じく ScriptableObject ではなく [SerializeReference] 対応の通常クラスとし、
    // ステップのリストにインスタンスとして直接保持される
    // 派生クラスを追加するときは PPTypeMenuName を必ず付けること（型選択ピッカーがこれに依存する）
    [Serializable]
    public abstract class PPUnitConditionValidator
    {
        [Header("表示")]
        [Label("説明")]
        [TextArea]
        [SerializeField] protected string mDescription;

        // 条件の説明文
        public string Description => mDescription;

        // 指定ユニットが条件を満たすか判定する
        // 派生クラスで実装する。状態を変えず判定のみを行うこと
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public abstract bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot);

        // 比較演算子を説明文用の日本語へ変換する
        // aOp : 比較演算子
        // return : 日本語の表記。未知の値は空文字
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

        // 条件リストを AND で評価する
        // 空リストは「条件なし」とみなして true を返す。null 要素は読み飛ばす
        // aConditions : 評価する条件リスト
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 全ての条件を満たす場合 true
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

        // 条件リストが 1 件でも中身を持っているか
        // 「達成済み判定条件が空なら常に未達成」の判定に使う
        // aConditions : 判定する条件リスト
        // return : null でない条件が 1 つでもあれば true
        public static bool HasAny(IReadOnlyList<PPUnitConditionValidator> aConditions)
        {
            if (aConditions == null) return false;

            foreach (var condition in aConditions)
            {
                if (condition != null) return true;
            }
            return false;
        }
    }
}
