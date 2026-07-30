/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleLog.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトル用ログ定義
 * =====================================*/

using System.Collections.Generic;

namespace CommandBattleCore
{
    /// <summary>
    /// バトルログの種別。UI 側で「戦闘ログ」を種類ごとに色分け・フィルタするために使う。
    /// </summary>
    public enum BattleLogType
    {
        /// <summary>コマンドの投入。</summary>
        Action,
        /// <summary>ダメージの発生。</summary>
        Damage,
        /// <summary>回復の発生。</summary>
        Heal,
        /// <summary>ステータスエフェクトの増減。</summary>
        StatusEffect,
        /// <summary>ユニットの撃破。</summary>
        UnitDefeated,
        /// <summary>メンバーの入れ替え。</summary>
        Swap,
        /// <summary>逃走。</summary>
        Escape,
        /// <summary>状態異常による行動失敗。</summary>
        ActionBlocked,
        /// <summary>その他。バトル終了通知などに使われる。</summary>
        Custom,
    }

    /// <summary>
    /// バトルログ 1 行分のエントリ。
    /// 表示用の文言だけでなく関係ユニットの参照も持つため、
    /// ログから対象を辿った演出やデバッグ表示にも使える。
    /// </summary>
    public record BattleLogEntry
    {
        /// <summary>ログ種別。</summary>
        public BattleLogType LogType { get; protected set; }
        /// <summary>行動主体のユニット。無い場合は null。</summary>
        public BattleUnit Unit { get; protected set; }
        /// <summary>対象のユニット。無い場合は null。</summary>
        public BattleUnit Target { get; protected set; }
        /// <summary>ログ本文。</summary>
        public string Description { get; protected set; }
        /// <summary>記録時刻。<see cref="BattleManager.TimeProvider"/> から取得した値。</summary>
        public float TimeStamp { get; protected set; }

        /// <param name="aType">ログ種別。</param>
        /// <param name="aSource">行動主体のユニット。</param>
        /// <param name="aTarget">対象のユニット。</param>
        /// <param name="aDescription">ログ本文。</param>
        /// <param name="aTimeStamp">記録時刻。</param>
        public BattleLogEntry(BattleLogType aType, BattleUnit aSource, BattleUnit aTarget, string aDescription,
            float aTimeStamp)
        {
            LogType = aType;
            Unit = aSource;
            Target = aTarget;
            Description = aDescription;
            TimeStamp = aTimeStamp;
        }
    }

    /// <summary>
    /// バトルログの出力先。<see cref="BattleManager.Logger"/> に差し込む。
    /// 実装を差し替えれば、履歴保持のかわりに UI へ即時表示したりファイルへ書き出したりできる。
    /// </summary>
    public interface IBattleLogger
    {
        /// <summary>ログを 1 件出力する。</summary>
        /// <param name="entry">出力するログエントリ。</param>
        void Log(BattleLogEntry entry);
    }

    /// <summary>
    /// 標準のロガー。出力せずメモリ上に履歴として溜めるだけの実装。
    /// リザルト画面での戦闘ログ表示などに使う。
    /// </summary>
    public class DefaultBattleLogger : IBattleLogger
    {
        /// <summary>記録された全ログ。</summary>
        protected readonly List<BattleLogEntry> mHistory = new();
        /// <summary>ログ履歴の読み取り専用ビュー。</summary>
        public IReadOnlyList<BattleLogEntry> History => mHistory;
        /// <summary>ログを履歴へ追加する。</summary>
        /// <param name="entry">追加するログエントリ。</param>
        public virtual void Log(BattleLogEntry entry) => mHistory.Add(entry);
    }
}
