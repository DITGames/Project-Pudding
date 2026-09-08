/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file DamageInfo.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ダメージ情報定義
 * =====================================*/
using System;
using UnityEngine;

namespace CommandBattleCore
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

    // そのダメージが何によって発生したか
    // 「攻撃を受けたときだけ消費する」といった、ダメージの出どころで挙動を変えたい場合に参照する
    public enum DamageReason
    {
        // 通常攻撃・攻撃スキルなど、他者の攻撃行動によるダメージ
        [InspectorName("攻撃")]
        Attack,
        // 毒などステータスエフェクトによる継続ダメージ
        [InspectorName("継続ダメージ")]
        StatusEffect,
        // 反撃・とげなど、リアクションとして発生したダメージ
        // 攻撃ではあるが Attack とは区別する。反撃に反撃を返さないための判定に使う
        [InspectorName("反撃")]
        Reaction,
        // 上記のいずれでもないもの（環境ダメージ・デバッグ用など）
        [InspectorName("その他")]
        Other,
    }

    // 1 回分のダメージに関する情報をまとめて持ち回るクラス
    // 命中判定からダメージ適用までの各段階でこのインスタンスが引き渡され、
    // ステータスエフェクトやイベント購読側が Amount や各フラグを
    // 書き換えることで軽減・無効化を表現する
    // （そのため値型ではなく参照型で、値の書き換えを前提にしている）
    public class DamageInfo
    {
        // ダメージの発生元ユニット
        // コマンド以外で不用意に Source にユニットを格納するのは避ける
        // 反射ダメージなどで Source に Unit を入れてしまうと、
        // 反射が反射を呼ぶループが走るため
        public BattleUnit Source { get; }
        // ダメージを受けるユニット
        public BattleUnit Target { get; }
        // ダメージ量。適用前の介入で書き換えられる
        public float Amount { get; set; }
        // ダメージの発生源となったスキル定義やエフェクトなど
        public object SourceAbility { get; set; }
        // 何によって発生したダメージか
        // 大半のダメージは攻撃由来のため既定値は Attack。継続ダメージ等を作る側が明示的に変更する
        // Attack のダメージがリアクション実行中に発生した場合は、BattleUnit.ApplyDamage が Reaction へ上書きする
        public DamageReason Reason { get; set; } = DamageReason.Attack;

        // クリティカルヒットしたか
        public bool IsCritical { get; set; } = false;
        // 無効化されたか。true なら Amount によらずダメージは入らない
        public bool IsNullified { get; set; } = false;
        // 攻撃が外れたか。true なら軽減処理を通さず結果通知のみ行われる
        public bool IsMiss { get; set; } = false;

        // ダメージ情報を生成する
        // aSource : ダメージの発生元ユニット。無い場合は null
        // aTarget : ダメージを受けるユニット
        // aAmount : 初期ダメージ量
        // aSourceAbility : 発生源のスキル定義やエフェクト
        // aReason : 何によって発生したダメージか
        public DamageInfo(BattleUnit aSource, BattleUnit aTarget, float aAmount, object aSourceAbility = null,
            DamageReason aReason = DamageReason.Attack)
        {
            Source = aSource;
            Target = aTarget;
            Amount = aAmount;
            SourceAbility = aSourceAbility;
            Reason = aReason;
        }
    }
}
