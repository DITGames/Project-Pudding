/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file AttackSkillDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief 攻撃系スキルのベース定義
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CommandBattleCore
{
    // 攻撃スキルの種別。参照するパラメータや耐性の扱いを分けるために使う
    public enum AttackCategory
    {
        Physical,
        Magic
    }

    // 対象にダメージを与える攻撃スキルの定義
    // 効果は「攻撃力 + スキルパワー - 防御力 × 0.5」で最低 1 ダメージを保証し、
    // 対象ごとに命中判定とクリティカル補正を通してから適用する
    // AttackCommand の計算式とほぼ同じだが、スキルパワーが上乗せされる点が違う
    [CreateAssetMenu(fileName = "AttackSkillDefinition", menuName = "CommandBattleCore/AttackSkillDefinition")]
    public class AttackSkillDefinition : SkillDefinition
    {
        [Label("種別")]
        [SerializeField] protected AttackCategory mCategory;
        public AttackCategory Category => mCategory;

        // 対象全員にダメージを与える効果を組み立てる
        // 生成されるデリゲートは実行のたびに、対象ごとのダメージ算出 → 命中判定 →
        // クリティカル補正 → 適用を繰り返す
        // return : 効果本体のデリゲート
        protected override Action<BattleUnit, List<BattleUnit>, BattleContext> BuildEffect()
        {
            return (src, targets, ctx) =>
            {
                foreach (var t in targets)
                {
                    float dmg = Math.Max(1f, src.Parameters.Attack.CurrentValue + mPower
                                             - t.Parameters.Defense.CurrentValue * 0.5f);

                    var damageInfo = new DamageInfo(src, t, dmg, this);
                    var hit = ctx.ResolveHit(src, t, damageInfo);

                    if (hit.mResult == HitResult.Miss)
                    {
                        damageInfo.IsMiss = true;
                        damageInfo.Amount = 0f;
                    }

                    if (hit.mCriticalInfo.IsCritical)
                    {
                        damageInfo.IsCritical = true;
                        damageInfo.Amount *= hit.mCriticalInfo.CriticalMultiplier;
                    }

                    t.ApplyDamage(damageInfo);
                }
            };
        }
    }
}
