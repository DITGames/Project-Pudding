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
    // Project-Pudding 固有の要素を載せたバトルユニット
    // 汎用の BattleUnit に対して、属性・拡張パラメータ・AI 用のロール／知能／スコア補正を追加する
    // AI 側（PPPartyAIStrategistBase）は行動候補のスコアリングでここの
    // AssignedRole と Intelligence を参照する
    public class PPBattleUnit : BattleUnit
    {
        // 基底の BattleUnit.Parameters に含まれない、本作固有の追加パラメータ
        public PPParameterSet ExtraParameters { get; }

        // AI 上の役割。PPUnitRole.Inherit ならパーティ側の設定を継承する
        public PPUnitRole AssignedRole { get; set; } = PPUnitRole.Inherit;
        // このユニット固有の行動スコア補正。AI のスコアリングに掛かる
        public PPUnitActionScoreModifier ScoreModifier { get; set; } = new PPUnitActionScoreModifier();

        // AI の賢さ（0〜1）。低いほど最適でない行動を選びやすくなる
        // 0 はパーティプロファイルの Intelligence を継承する意味
        public float Intelligence { get; set; } = 0f;

        // ユニットの属性。弱点・耐性倍率の判定に使う
        public PPTypeAttribute TypeAttribute { get; }

        // aUnitId : ユニットID
        // aDisplayName : UI表示名
        // aParameterSet : 基本パラメータ一式
        // aExtraParameterSet : 本作固有の追加パラメータ一式
        // aTypeAttribute : ユニットの属性
        public PPBattleUnit(string aUnitId, string aDisplayName, ParameterSet aParameterSet,
            PPParameterSet aExtraParameterSet, PPTypeAttribute aTypeAttribute)  : base(aUnitId, aDisplayName, aParameterSet)
        {
            ExtraParameters = aExtraParameterSet;
            TypeAttribute = aTypeAttribute;
        }

        // 今の状況で発動できるスキルを 1 つでも持っているかを判定する
        // リソース不足やクールダウンで全スキルが撃てない「手詰まり」の検出に使う
        // aContext : 発動可否の検証に使うバトルコンテキスト
        // return : 発動可能なスキルが 1 つでもあれば true
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

        // ID からパラメータを解決する。基本パラメータを先に探し、無ければ拡張パラメータを探す
        // aId : パラメータID
        // return : 該当パラメータ。どちらにも存在しなければ null
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
