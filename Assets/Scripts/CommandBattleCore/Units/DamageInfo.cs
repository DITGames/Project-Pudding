/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file DamageInfo.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ダメージ情報定義
 * =====================================*/
using System;

namespace CommandBattleCore
{
    /// <summary>
    /// ダメージの性質を表すフラグ。耐性・弱点の判定に使う。
    /// </summary>
    [Flags]
    public enum DamageTags
    {
        /// <summary>指定なし。</summary>
        None = 0,
        /// <summary>物理ダメージ。</summary>
        Physical = 1 << 0,
        /// <summary>魔法ダメージ。</summary>
        Magical = 1 << 1,
        // 属性などを拡張
    }

    /// <summary>
    /// 1 回分のダメージに関する情報をまとめて持ち回るクラス。
    /// <para>
    /// 命中判定からダメージ適用までの各段階でこのインスタンスが引き渡され、
    /// ステータスエフェクトやイベント購読側が <see cref="Amount"/> や各フラグを
    /// 書き換えることで軽減・無効化を表現する
    /// （そのため値型ではなく参照型で、値の書き換えを前提にしている）。
    /// </para>
    /// </summary>
    public class DamageInfo
    {
        /// <summary>
        /// ダメージの発生元ユニット。
        /// <para>
        /// コマンド以外で不用意に Source にユニットを格納するのは避ける。
        /// 反射ダメージなどで Source に Unit を入れてしまうと、
        /// 反射が反射を呼ぶループが走るため。
        /// </para>
        /// </summary>
        public BattleUnit Source { get; }
        /// <summary>ダメージを受けるユニット。</summary>
        public BattleUnit Target { get; }
        /// <summary>ダメージ量。適用前の介入で書き換えられる。</summary>
        public float Amount { get; set; }
        /// <summary>ダメージの発生源となったスキル定義やエフェクトなど。</summary>
        public object SourceAbility { get; set; }

        /// <summary>クリティカルヒットしたか。</summary>
        public bool IsCritical { get; set; } = false;
        /// <summary>無効化されたか。true なら <see cref="Amount"/> によらずダメージは入らない。</summary>
        public bool IsNullified { get; set; } = false;
        /// <summary>攻撃が外れたか。true なら軽減処理を通さず結果通知のみ行われる。</summary>
        public bool IsMiss { get; set; } = false;

        /// <param name="aSource">ダメージの発生元ユニット。無い場合は null。</param>
        /// <param name="aTarget">ダメージを受けるユニット。</param>
        /// <param name="aAmount">初期ダメージ量。</param>
        /// <param name="aSourceAbility">発生源のスキル定義やエフェクト。</param>
        public DamageInfo(BattleUnit aSource, BattleUnit aTarget, float aAmount, object aSourceAbility = null)
        {
            Source = aSource;
            Target = aTarget;
            Amount = aAmount;
            SourceAbility = aSourceAbility;
        }
    }
}
