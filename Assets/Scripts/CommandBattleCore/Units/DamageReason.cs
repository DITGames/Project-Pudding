/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file DamageReason.cs
 * @author hqrse
 * @date 2026/11/09
 * @brief ダメージの発生理由（何によるダメージか）の定義
 * =====================================*/

using UnityEngine;

namespace CommandBattleCore
{
    // そのダメージが何によって発生したかを表す理由の基底
    // 「リアクション中かどうか」は別軸として DamageInfo.Reaction が持つため、ここでは扱わない
    // 発生源（コマンド・スキル定義・ステータスエフェクトなど）は派生クラスが型付きで持つ
    public abstract class DamageReasonBase
    {
    }

    // 通常攻撃・攻撃スキルなど、攻撃行動によるダメージ
    public class AttackDamageReason : DamageReasonBase
    {
        // 発生元のコマンド。コマンド外から発生した場合など、不明なら null
        public BattleCommandBase Command { get; }
        // 発生元のスキル定義。通常攻撃なら null
        public SkillDefinition Skill { get; }

        // aCommand : 発生元のコマンド。不明なら null
        // aSkill : 発生元のスキル定義。通常攻撃なら null
        public AttackDamageReason(BattleCommandBase aCommand, SkillDefinition aSkill = null)
        {
            Command = aCommand;
            Skill = aSkill;
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
        // aReason : 判定するダメージ理由
        // aKind : 照合する種類
        // return : 当たる場合 true
        public static bool Matches(DamageReasonBase aReason, DamageReasonKind aKind)
            => aKind switch
            {
                DamageReasonKind.Attack => aReason is AttackDamageReason,
                DamageReasonKind.StatusEffect => aReason is StatusEffectDamageReason,
                DamageReasonKind.Other => aReason is not AttackDamageReason && aReason is not StatusEffectDamageReason,
                _ => false,
            };
    }
}
