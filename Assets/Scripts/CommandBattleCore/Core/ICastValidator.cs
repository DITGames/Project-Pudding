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

namespace CommandBattleCore
{
    // スキルが発動できなかった理由
    // BattleContext.OnCastFailed を通じて UI へ伝わり、失敗表示の出し分けに使う
    public enum CastFailReason
    {
        // 理由なし（成功時、または分類不能）
        None,
        // クールダウン中
        OnCooldown,
        // 1 戦闘あたりの使用回数を使い切った
        MaxUses,
        // 有効な対象が居ない
        InvalidTarget,
        // コア実装のデフォルトはここまで
        // ここから下は採用先が独自に使う理由
        InvalidDefinition,
        InvalidParty,
        NotEnoughResource,
    }

    // スキル発動可否の判定結果。可否と、不可の場合の理由を持つ
    public readonly struct CastValidation
    {
        // 発動できるか
        public bool CanCast { get; }
        // 発動できない場合の理由
        public CastFailReason Reason { get; }

        // 判定結果を生成する
        // aCanCast : 発動可否
        // aReason : 不可の場合の理由
        public CastValidation(bool aCanCast, CastFailReason aReason = CastFailReason.None)
        {
            CanCast = aCanCast;
            Reason = aReason;
        }

        // 発動可能を表す共有インスタンス
        public static readonly CastValidation Ok = new(true);
        // 理由付きの発動不可を生成する
        // reason : 発動できない理由
        public static CastValidation Fail(CastFailReason reason) => new(false, reason);
    }

    // スキルを発動できるかを検証するバリデータ。BattleRules.CastValidator に差し込む
    // AI の候補絞り込みや UI のグレーアウト判定にも同じものが使われるため、
    // ここでは状態を変えず、判定だけを行うこと
    public interface ICastValidator
    {
        // 発動可否を検証する
        // aUser : 発動しようとしているユニット
        // aSkill : 対象のスキル
        // aContext : バトルコンテキスト
        // return : 判定結果
        CastValidation Validate(BattleUnit aUser, BattleSkill aSkill, BattleContext aContext);
    }

    // コア標準のバリデータ。クールダウンと使用回数のみを見る
    public class DefaultCastValidator : ICastValidator
    {
        // スキルが使用可能な状態かを検証し、不可なら理由を特定して返す
        // aUser : 発動しようとしているユニット
        // aSkill : 対象のスキル
        // aContext : バトルコンテキスト
        // return : 判定結果
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
