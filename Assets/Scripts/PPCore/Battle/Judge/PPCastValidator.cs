/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPCastValidator.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief スキル発動可否の検証
 *
 * @update
 * 6/21 CastFailReasonの追加
 * =====================================*/

namespace PPCore
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
        // 標準のバリデータが返すのはここまで（Silenced を除く）
        // ここから下は採用先が独自に使う理由
        InvalidDefinition,
        InvalidParty,
        NotEnoughResource,
        // 発動者にスキル使用不可（ActionRestriction.Silenced）の制限が掛かっている。標準のバリデータが返す
        // 既存の値の整数がずれないよう末尾に追加している
        Silenced,
    }

    // スキル発動可否の判定結果。可否と、不可の場合の理由を持つ
    public readonly struct CastValidation
    {
        // 発動できるか
        public bool CanCast { get; }
        // 発動できない場合の理由
        public CastFailReason Reason { get; }

        // 判定結果を生成する
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
    // AI や UI からも呼ばれるため、状態を変えず判定だけを行うこと
    public interface ICastValidator
    {
        // 発動可否を検証する
        CastValidation Validate(BattleUnit aUser, BattleSkill aSkill, BattleContext aContext);
    }

    // 標準のバリデータ。発動者のスキル使用不可の制限と、クールダウン・使用回数を見る
    public class DefaultCastValidator : ICastValidator
    {
        // スキルが使用可能な状態かを検証し、不可なら理由を特定して返す
        // 発動者側の制限（スキル使用不可）を、スキル側の状態（クールダウン等）より先に見る
        public virtual CastValidation Validate(BattleUnit aUser, BattleSkill aSkill, BattleContext aContext)
        {
            // スキル使用不可の制限が掛かっていればスキルは撃てない（通常攻撃はこの検証を通らないため使える）
            if (aUser != null && (aUser.CurrentRestrictions & ActionRestriction.Silenced) != 0)
            {
                return CastValidation.Fail(CastFailReason.Silenced);
            }

            if (!aSkill.IsReady)
            {
                if(aSkill.IsLimit) return CastValidation.Fail(CastFailReason.MaxUses);
                if (aSkill.IsCooldown) return CastValidation.Fail(CastFailReason.OnCooldown);
                return CastValidation.Fail(CastFailReason.None);   // 構造的に来ないはずだが
            }

            return CastValidation.Ok;
        }
    }

    // クールダウンと使用回数に加えて、定義の解決可否・発動者の型・スキルゲージ残量を検証するバリデータ
    // 残量の確認のみで消費は行わない（消費は PPSkillCommand.Execute が行う）
    public class PPBattleCastValidator : DefaultCastValidator
    {
        // 発動可否を検証する。基底の判定を先に通し、通過した場合のみ固有条件を順に確認する
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
            // ゲージを持たないユニットはスキルを撃てない
            if (aUser is not PPBattleUnit ppUser)
            {
                return CastValidation.Fail(CastFailReason.InvalidDefinition);
            }
            // スキルゲージ不足
            // 浮動小数の誤差で「ちょうど足りている」が不足扱いにならないよう、微小値を足して比較する
            if (ppUser.ExtraParameters.SkillGauge.Current + PPGaugeUtility.CompareEpsilon < def.SkillGaugeCost)
            {
                return CastValidation.Fail(CastFailReason.NotEnoughResource);
            }

            return CastValidation.Ok;
        }
    }
}
