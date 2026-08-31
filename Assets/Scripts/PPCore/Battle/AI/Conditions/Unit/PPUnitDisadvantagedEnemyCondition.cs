/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitDisadvantagedEnemyCondition.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief ユニット条件 : 不利属性の敵がいる
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ユニット条件: 自分に対して有利な属性を持つ敵が生存しているか
    //
    // 弱点を突ける敵がいるか（PPUnitCanExploitWeaknessCondition）の裏返しにあたる条件で、
    // 「相性の悪い相手が居るうちは守りに回る」「不利な相手から逃げる」といった判断に使う
    // 判定は相性表を持つ PPAttributeAffinity に委ね、ここへ相性の知識を持ち込まない
    [Serializable]
    [PPTypeMenuName("戦況/不利属性の敵がいる")]
    public sealed class PPUnitDisadvantagedEnemyCondition : PPUnitConditionValidator
    {
        // 反転すると「不利な相手が居ない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 自分を弱点として突ける敵が居るかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            bool isFound = false;
            foreach (var enemy in aSnapShot.AliveEnemies)
            {
                // 攻撃側が敵、防御側が自分。敵から見て自分が弱点なら不利な相手
                if (PPAttributeAffinity.Resolve(enemy.TypeAttribute, aUnit.TypeAttribute) != PPAffinityResult.Weak)
                    continue;

                isFound = true;
                break;
            }
            return isFound != mIsInvert;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
            => mDescription = mIsInvert ? "不利属性の敵がいない" : "不利属性の敵がいる";
    }
}
