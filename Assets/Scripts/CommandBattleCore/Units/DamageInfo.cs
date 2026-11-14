/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file DamageInfo.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ダメージ情報定義
 * =====================================*/
using System;

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
        // 何によって発生したダメージか。発生源（コマンド・スキル定義・エフェクトなど）も派生クラスが持つ
        // 生成時に決まり、リアクション中に発生しても書き換えない。null にはならない
        public DamageReasonBase Reason { get; }
        // リアクション（反撃など）として発生したダメージなら、その深さと発生元。null ならリアクションではない
        // 作る側が設定しなければ、リアクションのコマンド実行中に BattleUnit.ApplyDamage が設定する
        public ReactionDamageInfo Reaction { get; set; }
        // リアクションとして発生したダメージなら true
        public bool IsReaction => Reaction != null;

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
        // aReason : 何によって発生したダメージか。null なら OtherDamageReason.Default として扱う
        public DamageInfo(BattleUnit aSource, BattleUnit aTarget, float aAmount, DamageReasonBase aReason)
        {
            Source = aSource;
            Target = aTarget;
            Amount = aAmount;
            Reason = aReason ?? OtherDamageReason.Default;
        }
    }
}
