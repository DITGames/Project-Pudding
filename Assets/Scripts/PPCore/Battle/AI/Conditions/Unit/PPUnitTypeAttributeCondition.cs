/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitTypeAttributeCondition.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief ユニット条件 : 属性の一致
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ユニット条件: ユニットの属性が指定と一致するか
    // 属性相性は弱点・耐性の判定に効くため、
    // 「弱点を突ける属性のユニットにだけ大技を撃たせる」といった絞り込みに使う
    [Serializable]
    [PPTypeMenuName("ユニット状態/属性")]
    public sealed class PPUnitTypeAttributeCondition : PPUnitConditionValidator
    {
        [Label("対象属性")]
        [SerializeField] private PPTypeAttribute mTypeAttribute = PPTypeAttribute.Normal;
        // 反転すると「その属性ではない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // ユニットの属性が指定と一致するかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
            => aUnit != null && (aUnit.TypeAttribute == mTypeAttribute) != mIsInvert;

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
        {
            string type = GetResourceTypeString(mTypeAttribute);
            mDescription = mIsInvert ? $"属性が{type}ではない" : $"属性が{type}";
        }

        // 属性を説明文用の日本語へ変換する。表示名は定数から引くためハードコードしない
        // aTypeAttribute : 対象の属性
        // return : 日本語の表記。未知の値は空文字
        private static string GetResourceTypeString(PPTypeAttribute aTypeAttribute)
            => aTypeAttribute switch
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
