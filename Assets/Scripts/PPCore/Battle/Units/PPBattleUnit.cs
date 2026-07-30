/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleUnit.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief バトルユニットのベースクラス
 * =====================================*/
using CommandBattleCore;

namespace PPCore
{
    /// <summary>
    /// Project-Pudding 固有の要素を載せたバトルユニット。
    /// <para>
    /// 汎用の <see cref="BattleUnit"/> に対して、属性・拡張パラメータ・AI 用のロール／知能／スコア補正を追加する。
    /// AI 側（<see cref="PPPartyAIStrategistBase"/>）は行動候補のスコアリングでここの
    /// <see cref="AssignedRole"/> と <see cref="Intelligence"/> を参照する。
    /// </para>
    /// </summary>
    public class PPBattleUnit : BattleUnit
    {
        /// <summary>基底の <see cref="BattleUnit.Parameters"/> に含まれない、本作固有の追加パラメータ。</summary>
        public PPParameterSet ExtraParameters { get; }

        /// <summary>AI 上の役割。<see cref="PPUnitRole.Inherit"/> ならパーティ側の設定を継承する。</summary>
        public PPUnitRole AssignedRole { get; set; } = PPUnitRole.Inherit;
        /// <summary>このユニット固有の行動スコア補正。AI のスコアリングに掛かる。</summary>
        public PPUnitActionScoreModifier ScoreModifier { get; set; } = new PPUnitActionScoreModifier();

        /// <summary>
        /// AI の賢さ（0〜1）。低いほど最適でない行動を選びやすくなる。
        /// 負値はパーティの Intelligence を継承する意味。
        /// </summary>
        public float Intelligence { get; set; } = -1f;

        /// <summary>ユニットの属性。弱点・耐性倍率の判定に使う。</summary>
        public PPTypeAttribute TypeAttribute { get; }

        /// <param name="aUnitId">ユニットID。</param>
        /// <param name="aDisplayName">UI表示名。</param>
        /// <param name="aParameterSet">基本パラメータ一式。</param>
        /// <param name="aExtraParameterSet">本作固有の追加パラメータ一式。</param>
        /// <param name="aTypeAttribute">ユニットの属性。</param>
        public PPBattleUnit(string aUnitId, string aDisplayName, ParameterSet aParameterSet,
            PPParameterSet aExtraParameterSet, PPTypeAttribute aTypeAttribute)  : base(aUnitId, aDisplayName, aParameterSet)
        {
            ExtraParameters = aExtraParameterSet;
            TypeAttribute = aTypeAttribute;
        }

        /// <summary>
        /// 今の状況で発動できるスキルを 1 つでも持っているかを判定する。
        /// リソース不足やクールダウンで全スキルが撃てない「手詰まり」の検出に使う。
        /// </summary>
        /// <param name="aContext">発動可否の検証に使うバトルコンテキスト。</param>
        /// <returns>発動可能なスキルが 1 つでもあれば true。</returns>
        public bool CanValidateSkill(BattleContext aContext)
        {
            foreach (var skill in Skills)
            {
                var result = aContext.Rules.CastValidator.Validate(this, skill, aContext);
                if (result.CanCast)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// ID からパラメータを解決する。基本パラメータを先に探し、無ければ拡張パラメータを探す。
        /// </summary>
        /// <param name="aId">パラメータID。</param>
        /// <returns>該当パラメータ。どちらにも存在しなければ null。</returns>
        public Parameter ResolveParameter(string aId)
        {
            if(Parameters.Parameters.TryGetValue(aId, out var paramDef))
            {
                return paramDef;
            }

            if (ExtraParameters.Parameters.TryGetValue(aId, out var paramEx))
            {
                return paramEx;
            }

            return null;
        }
    }
}
