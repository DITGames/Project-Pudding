/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPDamageInfo.cs
 * @author hqrse
 * @date 2026/07/27
 * @brief ダメージ情報とダメージ理由
 * =====================================*/

using System;
using UnityEngine;

namespace PPCore
{
    // ダメージの性質を表すフラグ。耐性・弱点の判定に使う
    [Flags]
    public enum DamageTags
    {
        // 指定なし
        None = 0,
        // 物理ダメージ
        Physical = 1 << 0,
        // 魔法ダメージ
        Magical = 1 << 1,
        // 属性などを拡張
    }

    // 1 回分のダメージに関する情報を持ち回るクラス
    // ステータスエフェクトや購読側が Amount やフラグを書き換えて軽減・無効化を表現するため、参照型にしている
    public class DamageInfo
    {
        /* 状態 */

        // ダメージの発生元ユニット
        // 反射ダメージなどコマンド以外で設定すると、反射が反射を呼ぶループになるため避ける
        public BattleUnit Source { get; }
        // ダメージを受けるユニット
        public BattleUnit Target { get; }
        // ダメージ量。適用前の介入で書き換えられる
        public float Amount { get; set; }
        // 何によって発生したダメージか。発生源（コマンド・エフェクトなど）も派生クラスが持つ
        // 生成時に決まり、リアクション中に発生しても書き換えない。null にはならない
        public DamageReasonBase Reason { get; }
        // リアクション（反撃など）として発生したダメージなら、その深さと発生元。null ならリアクションではない
        // 作る側が設定しなければ、リアクションのコマンド実行中に BattleUnit.ApplyDamage が設定する
        public ReactionDamageInfo Reaction { get; set; }
        // クリティカルヒットしたか
        public bool IsCritical { get; set; } = false;
        // 無効化されたか。true なら Amount によらずダメージは入らない
        public bool IsNullified { get; set; } = false;
        // 攻撃が外れたか。true なら軽減処理を通さず結果通知のみ行われる
        public bool IsMiss { get; set; } = false;

        /* プロパティ */

        // リアクションとして発生したダメージなら true
        public bool IsReaction => Reaction != null;

        /* 生成 */

        // ダメージ情報を生成する
        public DamageInfo(BattleUnit aSource, BattleUnit aTarget, float aAmount, DamageReasonBase aReason)
        {
            Source = aSource;
            Target = aTarget;
            Amount = aAmount;
            Reason = aReason ?? OtherDamageReason.Default;
        }
    }

    // そのダメージが何によって発生したかを表す理由の基底。発生源は派生クラスが型付きで持つ
    // リアクション中かどうかは別軸として DamageInfo.Reaction が持つ
    public abstract class DamageReasonBase
    {
    }

    // 通常攻撃など、攻撃行動によるダメージ
    public class AttackDamageReason : DamageReasonBase
    {
        // 発生元のコマンド。コマンド外から発生した場合など、不明なら null
        public BattleCommandBase Command { get; }

        // aCommand : 発生元のコマンド。不明なら null
        public AttackDamageReason(BattleCommandBase aCommand)
        {
            Command = aCommand;
        }
    }

    // 毒などステータスエフェクトによるダメージ
    // とげのような「エフェクト由来の反射」で追加の情報が要る場合は継承して拡張する
    public class StatusEffectDamageReason : DamageReasonBase
    {
        // 発生元のステータスエフェクト
        public StatusEffect Effect { get; }

        // aEffect : 発生元のステータスエフェクト
        public StatusEffectDamageReason(StatusEffect aEffect)
        {
            Effect = aEffect;
        }
    }

    // 攻撃・ステータスエフェクトのいずれでもないダメージ（環境ダメージ・デバッグ用など）
    public class OtherDamageReason : DamageReasonBase
    {
        // 状態を持たないため使い回す共有インスタンス
        public static readonly OtherDamageReason Default = new();
    }

    // ダメージ理由の大まかな種類。インスペクタで絞り込み条件を選ぶためだけに使う
    // 数値は旧 DamageReason（Attack=0 / StatusEffect=1 / Reaction=2 / Other=3）と同じにし、2（旧 Reaction）は欠番にしている
    public enum DamageReasonKind
    {
        // AttackDamageReason とその派生
        [InspectorName("攻撃")]
        Attack = 0,
        // StatusEffectDamageReason とその派生
        [InspectorName("継続ダメージ")]
        StatusEffect = 1,
        // 上記のどちらでもない理由すべて
        [InspectorName("その他")]
        Other = 3,
    }

    // DamageReasonKind とダメージ理由の型を照合するヘルパー
    public static class DamageReasonKindUtility
    {
        // ダメージ理由が指定した種類に当たるかを判定する
        public static bool Matches(DamageReasonBase aReason, DamageReasonKind aKind)
            => aKind switch
            {
                DamageReasonKind.Attack => aReason is AttackDamageReason,
                DamageReasonKind.StatusEffect => aReason is StatusEffectDamageReason,
                DamageReasonKind.Other => aReason is not AttackDamageReason && aReason is not StatusEffectDamageReason,
                _ => false,
            };
    }

    // 属性を持たせたダメージ情報
    // 弱点／耐性フラグは演出側がダメージ表示を出し分けるために使う
    public class PPDamageInfo : DamageInfo
    {
        /* 状態 */

        // 攻撃属性。相性判定の攻撃側として使われる
        public PPTypeAttribute Attribute { get; set; }
        // 弱点を突いたか。相性判定の結果として設定される
        public bool IsWeaknessHit { get; set; } = false;
        // 耐性で軽減されたか。相性判定の結果として設定される
        public bool IsResistHit { get; set; } = false;

        /* 生成 */

        public PPDamageInfo(BattleUnit aSource, BattleUnit aTarget, float aAmount,
            PPTypeAttribute aAttribute, DamageReasonBase aReason)
            : base(aSource, aTarget, aAmount, aReason)
        {
            Attribute = aAttribute;
        }
    }
}
