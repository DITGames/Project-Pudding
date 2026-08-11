/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleUnit.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief バトルユニットのベースクラス
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // Project-Pudding 固有の要素を載せたバトルユニット
    // 汎用の BattleUnit に対して、属性・拡張パラメータを追加する
    // 拡張パラメータの行動回数上限は、ティックごとに基底の ActionBudget へ同期される
    public class PPBattleUnit : BattleUnit
    {
        // 基底の BattleUnit.Parameters に含まれない、本作固有の追加パラメータ
        public PPParameterSet ExtraParameters { get; }

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

            // 最初のティックが来るまで既定値の 1 で据え置かれないよう、生成時点で上限を反映しておく
            Actions.Max = ResolveActionCount();
            Actions.ResetForTurn();
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

        // 1 ティック分の更新処理
        // 基底が「状態異常の更新 → 行動回数のリセット → クールダウン消化」を行うため、
        // バフ切れを反映した行動回数上限を使うには基底を通したあとで同期し直す必要がある
        // ActionBudget.ResetForTurn は残り回数を上限へ戻すだけなので、二度呼んでも副作用は無い
        // aContext : バトルコンテキスト
        public override void UnitTick(BattleContext aContext)
        {
            base.UnitTick(aContext);
            Actions.Max = ResolveActionCount();
            Actions.ResetForTurn();
        }

        // 1 ティックあたりの行動回数上限を解決する
        // バフデバフを載せた現在値を切り上げ、下限 1 で丸める（0 になって動けなくなることはない）
        // 切り上げなのは、端数の出るバフ（1.5 倍など）で回数が減って見えるのを避けるため
        // return : 行動回数上限
        public int ResolveActionCount()
        {
            var parameter = ExtraParameters.Get(PPParameterSet.ParameterIdActionCount);
            return parameter == null ? 1 : Mathf.Max(1, Mathf.CeilToInt(parameter.CurrentValue));
        }

        // ID からパラメータを解決する。基本パラメータを先に探し、無ければ拡張パラメータを探す
        // aId : パラメータID
        // return : 該当パラメータ。どちらにも存在しなければ null
        public override Parameter ResolveParameter(string aId)
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
