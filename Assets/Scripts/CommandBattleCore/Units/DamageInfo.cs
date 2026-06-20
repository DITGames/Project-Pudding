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
    [Flags]
    public enum DamageTags
    {
        None = 0,
        Physical = 1 << 0,
        Magical = 1 << 1,
        // 属性などを拡張
    }

    public class DamageInfo
    {
        // コマンド以外で不用意にSourceにユニットを格納するのを避ける
        // ※反射ダメージなどがあった場合にSourceにUnitを入れてしまうとループが走る
        public BattleUnit Source { get; }
        public BattleUnit Target { get; }
        public float Amount { get; set; }
        public DamageTags Tag { get; set; }
        // スキル定義やエフェクトなど
        public object SourceAbility { get; set; }

        // クリティカルフラグ
        public bool IsCritical { get; set; } = false;
        // 無効化フラグ
        public bool IsNullified { get; set; } = false;
        // ミスフラグ
        public bool IsMiss { get; set; } = false;

        public DamageInfo(BattleUnit aSource, BattleUnit aTarget, float aAmount, DamageTags aTag = DamageTags.None, object aSourceAbility = null)
        {
            Source = aSource;
            Target = aTarget;
            Amount = aAmount;
            Tag = aTag;
            SourceAbility = aSourceAbility;
        }
    }
}