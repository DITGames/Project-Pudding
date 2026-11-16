/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPartyConditionValidator.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief パーティ条件の基底と比較演算
 * =====================================*/

using System;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // パーティ AI の状況ルールが評価する条件の基底クラス
    // 「HP が減っている」「生存数が減っている」といった判定を 1 つずつクラス化し、
    // ビヘイビアツリー（PPUnitBehaviourTree）が複数を AND で束ねて判断のゲートにする
    // PPSkillEffectDefinition と同じく ScriptableObject ではなく [SerializeReference] 対応の通常クラスとし、
    // ツリーの条件リストにインスタンスとして直接保持される
    // 派生クラスを追加するときは PPTypeMenuName を必ず付けること（型選択ピッカーがこれに依存する）
    [Serializable]
    public abstract class PPPartyConditionValidator
    {
        [Header("表示")]
        // 設定内容から説明文を組み立て直すか
        // 外すと自動生成が止まり、書いた文面がそのまま残る
        // 自動生成は「何を見る条件か」を機械的に並べるだけなので、
        // 「開幕の入れ替え用」のような、その枝を置いた意図を書き残したい場合に外す
        [Label("説明を自動生成する")]
        [SerializeField] protected bool mIsAutoDescription = true;
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

        // 設定内容から説明文を組み立て直す
        // これまで右クリックメニューからしか呼べなかったため、設定を変えても説明文が古いままになっていた
        // 判断ツリーが編集のたびに呼ぶことで、グラフ上のサマリ表示と設定内容を一致させる
        // 自動生成を外している場合は書いた文面を残すため、何もしない
        public void RefreshDescription()
        {
            if (!mIsAutoDescription) return;

            BuildDescription();
        }

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

    // AI 条件アセットが共通で使う比較演算子
    // インスペクタ上で「HP割合が 30 以下なら」のような条件を組み立てるための選択肢
    public enum PPCompareOp
    {
        // 等しい
        [InspectorName("等しい")]
        Equal,
        // 等しくない
        [InspectorName("等しくない")]
        NotEqual,
        // 以上
        [InspectorName("以上")]
        GreaterOrEqual,
        // 以下
        [InspectorName("以下")]
        LessOrEqual,
        // より大きい
        [InspectorName("より大きい")]
        GreaterThan,
        // 未満
        [InspectorName("未満")]
        LessThan,
    }

    // 比較演算子を実際の判定へ落とし込むヘルパー
    // 全ての AI 条件アセットがここを経由することで、判定の挙動を揃えている
    public static class PPConditionMath
    {
        // 実数を比較する
        // 等値・非等値は浮動小数の誤差を吸収するため許容誤差込みで判定する
        // 許容誤差 0 のときは厳密な一致比較として振る舞う
        // aValue : 比較される値
        // aOp : 比較演算子
        // aThreshold : 閾値
        // aTolerance : 等値判定の許容誤差。既定は 0（厳密一致）
        // return : 条件を満たす場合 true
        public static bool Compare(float aValue, PPCompareOp aOp, float aThreshold, float aTolerance = 0f)
        => aOp switch
        {
            PPCompareOp.Equal => Mathf.Abs(aValue - aThreshold) <= aTolerance,
            PPCompareOp.NotEqual => Mathf.Abs(aValue - aThreshold) > aTolerance,
            PPCompareOp.GreaterOrEqual => aValue >= aThreshold,
            PPCompareOp.LessOrEqual => aValue <= aThreshold,
            PPCompareOp.GreaterThan => aValue > aThreshold,
            PPCompareOp.LessThan => aValue < aThreshold,
            _ => false
        };

        // 整数を比較する。実数版へ委譲するため判定の挙動は共通
        // 生存数やターン数のように厳密一致が要るケースで使うため、許容誤差は既定 0
        // aValue : 比較される値
        // aOp : 比較演算子
        // aThreshold : 閾値
        // aTolerance : 等値判定の許容誤差。既定は 0（厳密一致）
        // return : 条件を満たす場合 true
        public static bool Compare(int aValue, PPCompareOp aOp, int aThreshold, int aTolerance = 0)
         => Compare((float)aValue, aOp, (float)aThreshold, (float)aTolerance);
    }
}
