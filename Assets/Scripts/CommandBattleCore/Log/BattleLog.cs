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
    // バトルログの種別。UI 側で「戦闘ログ」を種類ごとに色分け・フィルタするために使う
    public enum BattleLogType
    {
        // コマンドの投入
        Action,
        // ダメージの発生
        Damage,
        // 回復の発生
        Heal,
        // ステータスエフェクトの増減
        StatusEffect,
        // ユニットの撃破
        UnitDefeated,
        // メンバーの入れ替え
        Swap,
        // 逃走
        Escape,
        // 状態異常による行動失敗
        ActionBlocked,
        // その他。バトル終了通知などに使われる
        Custom,
    }

    // バトルログ 1 行分のエントリ
    // 表示用の文言だけでなく関係ユニットの参照も持つため、
    // ログから対象を辿った演出やデバッグ表示にも使える
    public record BattleLogEntry
    {
        // ログ種別
        public BattleLogType LogType { get; protected set; }
        // 行動主体のユニット。無い場合は null
        public BattleUnit Unit { get; protected set; }
        // 対象のユニット。無い場合は null
        public BattleUnit Target { get; protected set; }
        // ログ本文
        public string Description { get; protected set; }
        // 記録時刻。BattleManager.TimeProvider から取得した値
        public float TimeStamp { get; protected set; }

        // aType : ログ種別
        // aSource : 行動主体のユニット
        // aTarget : 対象のユニット
        // aDescription : ログ本文
        // aTimeStamp : 記録時刻
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

    // バトルログの出力先。BattleManager.Logger に差し込む
    // 実装を差し替えれば、履歴保持のかわりに UI へ即時表示したりファイルへ書き出したりできる
    public interface IBattleLogger
    {
        // ログを 1 件出力する
        // entry : 出力するログエントリ
        void Log(BattleLogEntry entry);
    }

    // 標準のロガー。出力せずメモリ上に履歴として溜めるだけの実装
    // リザルト画面での戦闘ログ表示などに使う
    public class DefaultBattleLogger : IBattleLogger
    {
        // 記録された全ログ
        protected readonly List<BattleLogEntry> mHistory = new();
        // ログ履歴の読み取り専用ビュー
        public IReadOnlyList<BattleLogEntry> History => mHistory;
        // ログを履歴へ追加する
        public virtual void Log(BattleLogEntry entry) => mHistory.Add(entry);
    }
}
