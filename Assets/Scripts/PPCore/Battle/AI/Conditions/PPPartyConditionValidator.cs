/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyConditionValidator.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief パーティAI状況条件の基底クラス
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // パーティ AI の状況ルールが評価する条件の基底クラス
    // 「HP が減っている」「リソースが溜まっている」といった判定を 1 つずつクラス化し、
    // PPBattleTacticsDefinition が複数を AND で束ねて戦術の成立を判断する
    // PPSkillEffectDefinition と同じく ScriptableObject ではなく [SerializeReference] 対応の通常クラスとし、
    // PPBattleTacticsDefinition.Conditions にインスタンスとして直接保持される
    // 派生クラスを追加するときは PPTypeMenuName を必ず付けること（型選択ピッカーがこれに依存する）
    [Serializable]
    public abstract class PPPartyConditionValidator
    {
        [Header("表示")]
        [Label("説明")]
        [TextArea]
        [SerializeField] protected string mDescription;

        // 条件の説明文
        public string Description => mDescription;

        // 現在のスナップショットに対して条件を満たすか判定する
        // 派生クラスで実装する。状態を変えず判定のみを行うこと
        // aSnapShot : 評価対象のパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public abstract bool Evaluate(PPPartyAIContext aSnapShot);

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

        // 属性を説明文用の日本語へ変換する。表示名は定数から引くためハードコードしない
        // a : 対象の属性
        // return : 日本語の表記。未知の値は空文字
        protected string GetResourceTypeString(PPTypeAttribute a)
            => a switch
            {
                PPTypeAttribute.Normal => PPTypeAttributeDefinition.TypeNormal,
                PPTypeAttribute.Fire => PPTypeAttributeDefinition.TypeFire,
                PPTypeAttribute.Water => PPTypeAttributeDefinition.TypeWater,
                PPTypeAttribute.Earth => PPTypeAttributeDefinition.TypeEarth,
                PPTypeAttribute.Shine => PPTypeAttributeDefinition.TypeShine,
                PPTypeAttribute.Dark => PPTypeAttributeDefinition.TypeDark,
                _ => ""
            };
    }
}
