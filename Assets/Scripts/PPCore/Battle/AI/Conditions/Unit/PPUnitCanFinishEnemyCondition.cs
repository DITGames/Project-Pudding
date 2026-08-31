/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitCanFinishEnemyCondition.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニット条件 : 通常攻撃で倒せる敵がいる
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ユニット条件: このユニットの通常攻撃 1 回で倒しきれる敵がいるか
    // 「とどめを刺せるならスキルを温存して殴る」という判断の入口に使う
    // 見積もりに命中・クリティカルの乱数は含めないため、実際には落としきれないこともある
    [Serializable]
    [PPTypeMenuName("戦況/通常攻撃で倒せる敵がいる")]
    public sealed class PPUnitCanFinishEnemyCondition : PPUnitConditionValidator
    {
        // 反転すると「とどめを刺せる相手が居ない」の判定になる
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 通常攻撃で倒せる敵が居るかを判定する
        // aUnit : 判定対象のユニット
        // aSnapShot : 評価に使うパーティ状況スナップショット
        // return : 条件を満たす場合 true
        public override bool Evaluate(PPBattleUnit aUnit, PPPartyAIContext aSnapShot)
        {
            if (aUnit == null) return false;

            bool isFound = false;
            foreach (var enemy in aSnapShot.AliveEnemies)
            {
                if (PPDamageUtility.ResolveAttackDamage(aUnit, enemy) < enemy.Parameters.Hp.Current) continue;

                isFound = true;
                break;
            }
            return isFound != mIsInvert;
        }

        // 設定内容から説明文を組み立てる
        [ContextMenu("説明文を生成")]
        protected override void BuildDescription()
            => mDescription = mIsInvert ? "通常攻撃で倒せる敵がいない" : "通常攻撃で倒せる敵がいる";
    }
}
