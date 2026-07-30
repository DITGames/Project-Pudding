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
    /// <summary>
    /// 本作のスキルのランタイムインスタンス。
    /// <para>
    /// 現状は基底の <see cref="BattleSkill"/> に対して振る舞いを追加していないが、
    /// AI やコマンドが型で本作のスキルを判別できるよう、また今後の拡張点として分けてある。
    /// 属性やコストといった固有情報は <see cref="BattleSkill.SourceDefinition"/> に
    /// 入っている <see cref="PPSkillDefinition"/> 側から引く。
    /// </para>
    /// </summary>
    public class PPBattleSkill : BattleSkill
    {
        /// <param name="aSkillId">スキルID。</param>
        /// <param name="aDisplayName">UI表示名。</param>
        /// <param name="aDefaultResolver">既定のターゲットリゾルバ。</param>
        /// <param name="aEffect">効果本体のデリゲート。</param>
        public PPBattleSkill(string aSkillId, string aDisplayName, ITargetResolver aDefaultResolver,
            Action<BattleUnit, List<BattleUnit>, BattleContext> aEffect)
            : base(aSkillId, aDisplayName, aDefaultResolver, aEffect)
        {

        }
    }
}
