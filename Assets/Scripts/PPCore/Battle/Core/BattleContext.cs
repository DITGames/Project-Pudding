/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file BattleContext.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトルコンテキストと勝敗判定
 * =====================================*/

using System;
using System.Collections.Generic;

namespace PPCore
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

    // 1 バトル分の状態（両パーティ・ルール・ターン数・報酬・環境）を保持するコンテキスト
    // ターゲット解決と命中判定の入口も兼ねる
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

        // 実行中のコマンド。BattleManager がコマンドの効果を実行している間だけ設定し、それ以外は null
        // リアクション情報の引き継ぎや、発生元コマンドを辿る用途に使う
        public BattleCommandBase ExecutingCommand { get; protected internal set; }

        // スキル発動に失敗したとき(発動ユニット, スキル, 失敗理由)
        public event Action<BattleUnit, BattleSkill, CastFailReason> OnCastFailed;

        // スキル発動失敗を購読側へ通知する。コスト不足やクールダウン中の検出元から呼ばれる
        protected internal virtual void NotifyCastFailed(BattleUnit aUnit, BattleSkill aSkill, CastFailReason aReason)
            => OnCastFailed?.Invoke(aUnit, aSkill, aReason);

        // 指定した陣営のパーティを取得する
        public BattleParty GetParty(BattleSide aSide) =>
            aSide == BattleSide.Ally ? AllyParty : EnemyParty;

        // 指定した陣営から見た敵パーティを取得する
        public BattleParty GetOpponentParty(BattleSide aSide) =>
            aSide == BattleSide.Ally ? EnemyParty : AllyParty;

        // ターゲットを解決する。リゾルバで候補を出したあと、
        // Rules.TargetFilters を順に適用して最終的な対象リストへ絞り込む
        public List<BattleUnit> ResolveTargets(BattleUnit aSource, ITargetResolver aTargetResolver)
        {
            var result = aTargetResolver.Resolve(aSource, this);
            foreach (var filter in Rules.TargetFilters)
                result = filter.Filter(aSource, result, this);
            return result;
        }

        // 命中判定を行い、命中した場合のみ続けてクリティカル判定を行う
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

    // バトルの決着の種類。BattleResultType.InProgress 以外が返った時点で
    // BattleManager はバトルを終了させる
    public enum BattleResultType
    {
        // まだ決着していない
        InProgress,
        // 勝利。敵が全滅した
        Victory,
        // 敗北。味方が全滅した
        Defeat,
        // 引き分け。両陣営が同時に全滅した
        Draw,
        // 逃走による終了
        Escaped,
        // 拡張用。独自の終了条件を作る場合に使う
        Custom,
    }

    // バトルの結果
    // 現状は種別のみを持つが、リザルト画面向けの情報を足す場合はここを拡張する
    public class BattleResult
    {
        // 決着の種類
        public BattleResultType Type { get; }

        // type : 決着の種類
        public BattleResult(BattleResultType type)
        {
            Type = type;
        }

        // 「継続中」を表す共有インスタンス。毎回の判定で新規生成しないためのもの
        public static readonly BattleResult InProgress = new(BattleResultType.InProgress);
    }

    // 勝敗を判定するチェッカー。BattleRules.ResultChecker に差し込む
    // 「ボスだけ倒せば勝ち」「規定ターン生存で勝ち」といった条件を差し替えられるようにしてある
    public interface IBattleResultChecker
    {
        // 現在の状況から決着を判定する。ユニット撃破時やターン経過時など、頻繁に呼ばれる
        BattleResult CheckResult(BattleContext aContext);
    }

    // 標準の勝敗チェッカー。逃走フラグを最優先で見て、次に両陣営の全滅状況で判定する
    // 同時全滅は引き分けとして扱う
    public class DefaultBattleResultChecker : IBattleResultChecker
    {
        // 逃走 → 相打ち → 勝利 → 敗北の順に判定し、いずれにも当てはまらなければ継続中を返す
        public virtual BattleResult CheckResult(BattleContext aContext)
        {
            if (aContext.EscapeRequested)
                return new BattleResult(BattleResultType.Escaped);

            bool allyWiped = aContext.AllyParty.IsWiped();
            bool enemyWiped = aContext.EnemyParty.IsWiped();

            if (allyWiped && enemyWiped) return new BattleResult(BattleResultType.Draw);
            if (enemyWiped) return new BattleResult(BattleResultType.Victory);
            if (allyWiped) return new BattleResult(BattleResultType.Defeat);

            return BattleResult.InProgress;
        }
    }
}
