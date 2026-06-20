/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ICastProvider.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief スキル使用可否の判定インターフェース
 * =====================================*/
using UnityEngine;

namespace CommandBattleCore
{
    public enum CastFailReason
    {
        None,
        OnCooldown,
        MaxUses,
        InvalidTarget,
        // コア実装のデフォルトはここまで
    }

    public readonly struct CastValidation
    {
        public bool CanCast { get; }
        public CastFailReason Reason { get; }

        public CastValidation(bool aCanCast, CastFailReason aReason = CastFailReason.None)
        {
            CanCast = aCanCast;
            Reason = aReason;
        }

        public static readonly CastValidation Ok = new(true);
        public static CastValidation Fail(CastFailReason reason) => new(false, reason);
    }

    public interface ICastValidator
    {
        CastValidation Validate(BattleUnit aUser, BattleSkill aSkill, BattleContext aContext);
    }

    public class DefaultCastValidator : ICastValidator
    {
        public virtual CastValidation Validate(BattleUnit aUser, BattleSkill aSkill, BattleContext aContext)
        {
            if (!aSkill.IsReady)
            {
                if(aSkill.IsLimit) return CastValidation.Fail(CastFailReason.MaxUses);
                if (aSkill.IsCooldown) return CastValidation.Fail(CastFailReason.OnCooldown);
                return CastValidation.Fail(CastFailReason.None);   // 構造的に来ないはずだが
            }
            
            return CastValidation.Ok;
        }
    }
}