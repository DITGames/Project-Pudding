/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyConditionValidator.cs
 * @author hqrse
 * @date 2026/07/21
 * @brief パーティAI状況条件の基底クラス
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// パーティ AI の状況ルールが評価する条件の基底クラス（ScriptableObject）。
    /// <para>
    /// 「HP が減っている」「リソースが溜まっている」といった判定を 1 つずつアセット化し、
    /// <see cref="PPPartyAISituationRule"/> が複数を AND で束ねて状況を判断する。
    /// 条件をアセットにすることで、AI の性格付けをコードを触らず組み替えられる。
    /// </para>
    /// <para>
    /// 派生クラスを追加するときは <see cref="PPConditionMenuAttribute"/> と
    /// <c>CreateAssetMenu</c> を必ず付けること。ピッカー UI とアセット自動生成がこれに依存する。
    /// </para>
    /// </summary>
    public abstract class PPPartyConditionValidator : ScriptableObject
    {
        /// <summary>インスペクタに出す条件の説明文。設定内容から自動生成される想定。</summary>
        [Header("表示")]
        [Label("説明")]
        [TextArea]
        [SerializeField] protected string mDescription;

        /// <summary>条件の説明文。</summary>
        public string Description => mDescription;

        /// <summary>
        /// 現在のスナップショットに対して条件を満たすか判定する。
        /// 派生クラスで実装する。状態を変えず判定のみを行うこと。
        /// </summary>
        /// <param name="aSnapShot">評価対象のパーティ状況スナップショット。</param>
        /// <returns>条件を満たす場合 true。</returns>
        public abstract bool Evaluate(PPPartyAIContext aSnapShot);

        /// <summary>
        /// 比較演算子を説明文用の日本語へ変換する。
        /// </summary>
        /// <param name="aOp">比較演算子。</param>
        /// <returns>日本語の表記。未知の値は空文字。</returns>
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

        /// <summary>
        /// 設定内容から <see cref="mDescription"/> を組み立てる。
        /// 派生クラスでオーバーライドして、インスペクタ上で条件の意味が読めるようにする。
        /// </summary>
        protected virtual void BuildDescription()
        {
        }

        /// <summary>
        /// 属性を説明文用の日本語へ変換する。表示名は定数から引くためハードコードしない。
        /// </summary>
        /// <param name="a">対象の属性。</param>
        /// <returns>日本語の表記。未知の値は空文字。</returns>
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
