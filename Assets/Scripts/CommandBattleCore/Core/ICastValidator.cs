/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ICastProvider.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief スキル使用可否の判定インターフェース
 *
 * @update
 * 6/21 CastFailReasonの追加
 * =====================================*/
using UnityEngine;

namespace CommandBattleCore
{
    /// <summary>
    /// スキルが発動できなかった理由。
    /// <see cref="BattleContext.OnCastFailed"/> を通じて UI へ伝わり、失敗表示の出し分けに使う。
    /// </summary>
    public enum CastFailReason
    {
        /// <summary>理由なし（成功時、または分類不能）。</summary>
        None,
        /// <summary>クールダウン中。</summary>
        OnCooldown,
        /// <summary>1 戦闘あたりの使用回数を使い切った。</summary>
        MaxUses,
        /// <summary>有効な対象が居ない。</summary>
        InvalidTarget,
        // コア実装のデフォルトはここまで
        /// <summary>スキル定義の未解決。PPCore 側で使用。</summary>
        InvalidDefinition,
        /// <summary>パーティが不正。PPCore 側で使用。</summary>
        InvalidParty,
        /// <summary>リソース不足。PPCore 側で使用。</summary>
        NotEnoughResource,
    }

    /// <summary>
    /// スキル発動可否の判定結果。可否と、不可の場合の理由を持つ。
    /// </summary>
    public readonly struct CastValidation
    {
        /// <summary>発動できるか。</summary>
        public bool CanCast { get; }
        /// <summary>発動できない場合の理由。</summary>
        public CastFailReason Reason { get; }

        /// <param name="aCanCast">発動可否。</param>
        /// <param name="aReason">不可の場合の理由。</param>
        public CastValidation(bool aCanCast, CastFailReason aReason = CastFailReason.None)
        {
            CanCast = aCanCast;
            Reason = aReason;
        }

        /// <summary>発動可能を表す共有インスタンス。</summary>
        public static readonly CastValidation Ok = new(true);
        /// <summary>理由付きの発動不可を生成する。</summary>
        /// <param name="reason">発動できない理由。</param>
        public static CastValidation Fail(CastFailReason reason) => new(false, reason);
    }

    /// <summary>
    /// スキルを発動できるかを検証するバリデータ。<see cref="BattleRules.CastValidator"/> に差し込む。
    /// <para>
    /// AI の候補絞り込みや UI のグレーアウト判定にも同じものが使われるため、
    /// ここでは状態を変えず、判定だけを行うこと。
    /// </para>
    /// </summary>
    public interface ICastValidator
    {
        /// <summary>
        /// 発動可否を検証する。
        /// </summary>
        /// <param name="aUser">発動しようとしているユニット。</param>
        /// <param name="aSkill">対象のスキル。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>判定結果。</returns>
        CastValidation Validate(BattleUnit aUser, BattleSkill aSkill, BattleContext aContext);
    }

    /// <summary>
    /// コア標準のバリデータ。クールダウンと使用回数のみを見る。
    /// リソース消費のような固有仕様は <see cref="PPBattleCastValidator"/> 側で追加する。
    /// </summary>
    public class DefaultCastValidator : ICastValidator
    {
        /// <summary>
        /// スキルが使用可能な状態かを検証し、不可なら理由を特定して返す。
        /// </summary>
        /// <param name="aUser">発動しようとしているユニット。</param>
        /// <param name="aSkill">対象のスキル。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>判定結果。</returns>
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
