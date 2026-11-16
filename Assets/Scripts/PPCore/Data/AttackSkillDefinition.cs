/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file AttackSkillDefinition.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief 攻撃系スキルのベース定義
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 攻撃スキルの種別。参照するパラメータや耐性の扱いを分けるために使う
    public enum AttackCategory
    {
        Physical,
        Magic
    }

    // 対象にダメージを与える攻撃スキルの定義
    // 効果は「攻撃力 + スキルパワー - 防御力 × 0.5」で最低 1 ダメージ。対象ごとに命中判定とクリティカル補正を通す
    [CreateAssetMenu(fileName = "AttackSkillDefinition", menuName = "Project-Pudding/Skill/AttackSkillDefinition")]
    public class AttackSkillDefinition : SkillDefinition
    {
        /* インスペクタ設定 */

        [Label("種別")]
        [SerializeField] protected AttackCategory mCategory;
        // ダメージ量の基礎値になる
        [Label("スキルパワー")]
        [SerializeField] protected float mPower = 10f;

        /* プロパティ */

        public AttackCategory Category => mCategory;

        /* 生成 */

        // 対象全員にダメージを与える効果を組み立てる
        // 対象ごとに、ダメージ算出 → 命中判定 → クリティカル補正 → 適用を行う
        protected override Action<BattleUnit, List<BattleUnit>, BattleContext> BuildEffect()
        {
            return (src, targets, ctx) =>
            {
                foreach (var t in targets)
                {
                    float dmg = Math.Max(1f, src.Parameters.Attack.CurrentValue + mPower
                                             - t.Parameters.Defense.CurrentValue * 0.5f);

                    // 効果のデリゲートにはコマンドが渡らないため、発生元コマンドは実行中のコマンドから取る
                    var damageInfo = new DamageInfo(src, t, dmg, new AttackDamageReason(ctx.ExecutingCommand, this));
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

                    t.ApplyDamage(damageInfo, ctx);
                }
            };
        }
    }
}
