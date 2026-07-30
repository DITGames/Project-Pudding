/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleContext.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトル開始のコンテキスト
 * =====================================*/

using System;
using System.Collections.Generic;

namespace CommandBattleCore
{
    // 勝利時に得られる報酬をまとめて保持する。経験値・所持金・アイテムの受け渡し用の入れ物
    public class BattleReward
    {
        // 獲得経験値
        public int Experience { get; }
        // 獲得所持金
        public int Money { get; }
        // 獲得アイテム。型は採用先で決めるため object のまま持つ
        public List<object> Items { get; } = new();
    }

    // 戦闘フィールドの環境情報（天候・地形など）を表す拡張前提の空クラス
    // 環境によるダメージ補正などを入れる場合はここを継承して BattleContext.Environment に差し込む
    public class BattleEnvironment
    {

    }

    // 1 バトル分の状態をまとめて保持するコンテキスト
    // BattleManager が進行を担うのに対し、こちらは「今どういう状況か」を持つデータ側の中心
    // 両パーティ・ルール・ターン数・報酬・環境を抱え、ターゲット解決と命中判定の入口も兼ねる
    public class BattleContext
    {
        // プレイヤー側パーティ
        public BattleParty AllyParty { get; set; }
        // 敵側パーティ
        public BattleParty EnemyParty { get; set; }
        // 勝利報酬
        public BattleReward Reward { get; set; } = new();
        // 戦闘環境
        public BattleEnvironment Environment { get; set; } = new();
        // 命中・クリティカル・乱数・詠唱可否・ターゲットフィルタなどの差し替え可能なルール一式
        public BattleRules Rules { get; set; } = new BattleRules();

        // 逃走要求フラグ。DefaultBattleResultChecker がこれを見て逃走終了と判定する
        public bool EscapeRequested { get; set; }

        // 経過ターン数。BattleManager.AdvanceTick で加算される
        public int TurnCount { get; set; }

        // スキル発動に失敗したとき(発動ユニット, スキル, 失敗理由)
        public event Action<BattleUnit, BattleSkill, CastFailReason> OnCastFailed;

        // スキル発動失敗を購読側へ通知する。コスト不足やクールダウン中の検出元から呼ばれる
        // aUnit : 発動しようとしたユニット
        // aSkill : 対象のスキル
        // aReason : 失敗理由
        protected internal virtual void NotifyCastFailed(BattleUnit aUnit, BattleSkill aSkill, CastFailReason aReason)
            => OnCastFailed?.Invoke(aUnit, aSkill, aReason);

        // 指定した陣営のパーティを取得する
        // aSide : 取得したい陣営
        // return : その陣営のパーティ
        public BattleParty GetParty(BattleSide aSide) =>
            aSide == BattleSide.Ally ? AllyParty : EnemyParty;

        // 指定した陣営から見た敵パーティを取得する
        // aSide : 基準となる陣営
        // return : その陣営の敵側パーティ
        public BattleParty GetOpponentParty(BattleSide aSide) =>
            aSide == BattleSide.Ally ? EnemyParty : AllyParty;

        // ターゲットを解決する。リゾルバで候補を出したあと、
        // Rules.TargetFilters を順に適用して最終的な対象リストへ絞り込む
        // aSource : 行動主体のユニット
        // aTargetResolver : 対象候補を列挙するリゾルバ
        // return : フィルタ適用後の対象ユニットリスト
        public List<BattleUnit> ResolveTargets(BattleUnit aSource, ITargetResolver aTargetResolver)
        {
            var result = aTargetResolver.Resolve(aSource, this);
            foreach (var filter in Rules.TargetFilters)
                result = filter.Filter(aSource, result, this);
            return result;
        }

        // 命中判定を行い、命中した場合のみ続けてクリティカル判定を行う
        // aSource : 攻撃側ユニット
        // aTarget : 防御側ユニット
        // aDamageInfo : 判定対象のダメージ情報
        // return : 命中結果とクリティカル情報をまとめた HitInfo
        public HitInfo ResolveHit(BattleUnit aSource, BattleUnit aTarget, DamageInfo aDamageInfo)
        {
            HitInfo info = new();
            info.mResult = Rules.HitResolver.Resolve(aSource, aTarget, aDamageInfo, this);
            if (info.mResult == HitResult.Hit)
            {
                info.mCriticalInfo = Rules.CriticalResolver.Resolve(aSource, aTarget, aDamageInfo, this);
            }
            return info;
        }
    }
}
