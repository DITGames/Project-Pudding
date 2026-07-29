/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleContext.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトル開始のコンテキスト
 * =====================================*/
using System;
using System.Collections.Generic;
using System.Data;

namespace CommandBattleCore
{
    /// <summary>
    /// 勝利時に得られる報酬をまとめて保持する。経験値・所持金・アイテムの受け渡し用の入れ物。
    /// </summary>
    public class BattleReward
    {
        /// <summary>獲得経験値。</summary>
        public int Experience { get; }
        /// <summary>獲得所持金。</summary>
        public int Money { get; }
        /// <summary>獲得アイテム。型は採用先で決めるため object のまま持つ。</summary>
        public List<object> Items { get; } = new();
    }

    /// <summary>
    /// 戦闘フィールドの環境情報（天候・地形など）を表す拡張前提の空クラス。
    /// 環境によるダメージ補正などを入れる場合はここを継承して <see cref="BattleContext.Environment"/> に差し込む。
    /// </summary>
    public class BattleEnvironment
    {

    }

    /// <summary>
    /// 1 バトル分の状態をまとめて保持するコンテキスト。
    /// <para>
    /// <see cref="BattleManager"/> が進行を担うのに対し、こちらは「今どういう状況か」を持つデータ側の中心。
    /// 両パーティ・ルール・ターン数・報酬・環境を抱え、ターゲット解決と命中判定の入口も兼ねる。
    /// バトル中のあらゆる処理はこのコンテキストを引き回して参照する。
    /// </para>
    /// </summary>
    public class BattleContext
    {
        /// <summary>プレイヤー側パーティ。</summary>
        public BattleParty AllyParty { get; set; }
        /// <summary>敵側パーティ。</summary>
        public BattleParty EnemyParty { get; set; }
        /// <summary>勝利報酬。</summary>
        public BattleReward Reward { get; set; } = new();
        /// <summary>戦闘環境。</summary>
        public BattleEnvironment Environment { get; set; } = new();
        /// <summary>命中・クリティカル・乱数・詠唱可否・ターゲットフィルタなどの差し替え可能なルール一式。</summary>
        public BattleRules Rules { get; set; } = new BattleRules();

        /// <summary>逃走要求フラグ。<see cref="DefaultBattleResultChecker"/> がこれを見て逃走終了と判定する。</summary>
        public bool EscapeRequested { get; set; }

        /// <summary>経過ターン数。<see cref="BattleManager.AdvanceTick"/> で加算される。</summary>
        public int TurnCount { get; set; }

        /// <summary>スキル発動に失敗したとき(発動ユニット, スキル, 失敗理由)</summary>
        public event Action<BattleUnit, BattleSkill, CastFailReason> OnCastFailed;

        /// <summary>
        /// スキル発動失敗を購読側へ通知する。コスト不足やクールダウン中の検出元から呼ばれる。
        /// </summary>
        /// <param name="aUnit">発動しようとしたユニット。</param>
        /// <param name="aSkill">対象のスキル。</param>
        /// <param name="aReason">失敗理由。</param>
        protected internal virtual void NotifyCastFailed(BattleUnit aUnit, BattleSkill aSkill, CastFailReason aReason)
            => OnCastFailed?.Invoke(aUnit, aSkill, aReason);

        /// <summary>
        /// 指定した陣営のパーティを取得する。
        /// </summary>
        /// <param name="aSide">取得したい陣営。</param>
        /// <returns>その陣営のパーティ。</returns>
        public BattleParty GetParty(BattleSide aSide) =>
            aSide == BattleSide.Ally ? AllyParty : EnemyParty;

        /// <summary>
        /// 指定した陣営から見た敵パーティを取得する。
        /// </summary>
        /// <param name="aSide">基準となる陣営。</param>
        /// <returns>その陣営の敵側パーティ。</returns>
        public BattleParty GetOpponentParty(BattleSide aSide) =>
            aSide == BattleSide.Ally ? EnemyParty : AllyParty;

        /// <summary>
        /// ターゲットを解決する。リゾルバで候補を出したあと、
        /// <see cref="BattleRules.TargetFilters"/> を順に適用して最終的な対象リストへ絞り込む。
        /// </summary>
        /// <param name="aSource">行動主体のユニット。</param>
        /// <param name="aTargetResolver">対象候補を列挙するリゾルバ。</param>
        /// <returns>フィルタ適用後の対象ユニットリスト。</returns>
        public List<BattleUnit> ResolveTargets(BattleUnit aSource, ITargetResolver aTargetResolver)
        {
            var result = aTargetResolver.Resolve(aSource, this);
            foreach (var filter in Rules.TargetFilters)
                result = filter.Filter(aSource, result, this);
            return result;
        }

        /// <summary>
        /// 命中判定を行い、命中した場合のみ続けてクリティカル判定を行う。
        /// </summary>
        /// <param name="aSource">攻撃側ユニット。</param>
        /// <param name="aTarget">防御側ユニット。</param>
        /// <param name="aDamageInfo">判定対象のダメージ情報。</param>
        /// <returns>命中結果とクリティカル情報をまとめた <see cref="HitInfo"/>。</returns>
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
