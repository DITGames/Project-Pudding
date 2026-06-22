/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PusherBattleCastValidator.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief Pusherのキャストチェッカー
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PusherBattle
{
    public class PusherBattleCastValidator : DefaultCastValidator
    {
        public override CastValidation Validate(BattleUnit aUser, BattleSkill aSkill, BattleContext aContext)
        {
            var result = base.Validate(aUser, aSkill, aContext);

            // ベースでキャストが弾かれる
            if (!result.CanCast)
            {
                return result;
            }
            // スキル定義の未解決
            if (aSkill.SourceDefinition is not PusherBattleSkillDefinition def)
            {
                return CastValidation.Fail(CastFailReason.InvalidDefinition);
            }
            // パーティの不一致
            if (aContext.GetParty(aUser.Side) is not PusherBattleParty party)
            {
                return CastValidation.Fail(CastFailReason.InvalidParty);
            }
            // コスト不足
            if (!party.ResourcePool.CheckConsumeCoinResource(def.RequiredCoin))
            {
                return CastValidation.Fail(CastFailReason.NotEnoughResource);
            }

            return CastValidation.Ok;
        }
    }
}