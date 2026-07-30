/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleSkill.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief バトルスキル定義のベースクラス
 * =====================================*/
using System;
using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    // 本作のスキルのランタイムインスタンス
    // 現状は基底の BattleSkill に対して振る舞いを追加していないが、
    // AI やコマンドが型で本作のスキルを判別できるよう、また今後の拡張点として分けてある
    // 属性やコストといった固有情報は BattleSkill.SourceDefinition に
    // 入っている PPSkillDefinition 側から引く
    public class PPBattleSkill : BattleSkill
    {
        // aSkillId : スキルID
        // aDisplayName : UI表示名
        // aDefaultResolver : 既定のターゲットリゾルバ
        // aEffect : 効果本体のデリゲート
        public PPBattleSkill(string aSkillId, string aDisplayName, ITargetResolver aDefaultResolver,
            Action<BattleUnit, List<BattleUnit>, BattleContext> aEffect)
            : base(aSkillId, aDisplayName, aDefaultResolver, aEffect)
        {

        }
    }
}
