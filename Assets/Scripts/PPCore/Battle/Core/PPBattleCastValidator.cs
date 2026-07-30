/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleCastValidator.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief 攻撃キャストチェッカー
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // リソース消費を考慮するスキル発動バリデータ
    // 基底の DefaultCastValidator がクールダウンと使用回数を見るのに加えて、
    // 本作固有の条件（定義の解決可否・パーティ種別・リソース残量）を検証する
    // ここでは残量の確認のみで消費は行わない
    // UI のグレーアウト判定や AI の候補絞り込みからも呼ばれるため、状態を変えてはいけない
    // 実際の消費は PPSkillCommand.Execute が行う
    public class PPBattleCastValidator : DefaultCastValidator
    {
        // 発動可否を検証する。基底の判定を先に通し、通過した場合のみ固有条件を順に確認する
        // aUser : 発動しようとしているユニット
        // aSkill : 対象のスキル
        // aContext : バトルコンテキスト
        // return : 判定結果。不可の場合は理由付きで返す
        public override CastValidation Validate(BattleUnit aUser, BattleSkill aSkill, BattleContext aContext)
        {
            var result = base.Validate(aUser, aSkill, aContext);

            // ベースでキャストが弾かれる
            if (!result.CanCast)
            {
                return result;
            }
            // スキル定義の未解決
            if (aSkill.SourceDefinition is not PPSkillDefinition def)
            {
                return CastValidation.Fail(CastFailReason.InvalidDefinition);
            }
            // パーティの不一致
            if (aContext.GetParty(aUser.Side) is not PPBattleParty party)
            {
                return CastValidation.Fail(CastFailReason.InvalidParty);
            }
            // コスト不足
            if (!party.ResourcePool.CanPay(def.Cost))
            {
                return CastValidation.Fail(CastFailReason.NotEnoughResource);
            }

            return CastValidation.Ok;
        }
    }
}
