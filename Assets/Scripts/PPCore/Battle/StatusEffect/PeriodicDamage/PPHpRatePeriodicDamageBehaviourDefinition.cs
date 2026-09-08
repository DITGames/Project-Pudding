/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPHpRatePeriodicDamageBehaviourDefinition.cs
 * @author hqrse
 * @date 2026/09/04
 * @brief 最大HPの割合で毎ターンダメージを与える振る舞いのデータ定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 対象の最大HPに対する割合で毎ターンダメージを与える振る舞いの定義
    // 最大HPが高い相手ほど効くため、HPの多いボスにも通る毒として使える
    [Serializable]
    [PPTypeMenuName("継続ダメージ/HP割合")]
    public class PPHpRatePeriodicDamageBehaviourDefinition : PPStatusEffectBehaviourDefinition
    {
        [Header("HP割合継続ダメージ")]
        // 対象の最大HPに対する割合。0.05 で最大HPの 5% になる
        [Label("毎ターン割合")][Range(0f, 1f)]
        [SerializeField]protected float mHpRatePerTurn = 0.05f;
        [Label("属性")]
        [SerializeField]protected PPTypeAttribute mAttribute = PPTypeAttribute.Normal;

        // 表示と実行で扱いを揃えるため、常に 0～1 へ丸めた値を使う
        // （[Label] と [Range] が同居するとどちらの PropertyDrawer が使われるか保証されず、
        //   インスペクタ側でスライダーによる制限が効かない場合があるため）
        protected float HpRatePerTurn => Mathf.Clamp01(mHpRatePerTurn);

        // aEffect : 組み立て先のエフェクト
        // aSource : エフェクトの付与元ユニット
        // aTarget : 付与される対象ユニット
        // aContext : バトルコンテキスト
        public override void ConfigureBehaviour(StatusEffect aEffect, BattleUnit aSource, BattleUnit aTarget, BattleContext aContext)
        {
            aEffect.AddBehaviour(new PPHpRatePeriodicDamageBehaviour(HpRatePerTurn, mAttribute));
        }

        public override string BuildString()
            => $"継続ダメージ：最大HPの{HpRatePerTurn:P0}/ターン（{mAttribute}）";
    }
}
