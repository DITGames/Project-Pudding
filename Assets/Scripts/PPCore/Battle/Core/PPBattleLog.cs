/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleLog.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトルログ（種別・出力先・カスタムコンソールへの橋渡し）
 * =====================================*/

using System.Collections.Generic;
using CustomConsole;

namespace PPCore
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
    // 関係ユニットの参照も持つため、演出やデバッグ表示にも使える
    public record BattleLogEntry
    {
        /* 状態 */

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

        /* 生成 */

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

    // BattleManager.Logger として差し込む IBattleLogger 実装
    // Damage種別はダメージ発生源を含められないため、OnDamageResolved側で個別にログを出す
    public class PPBattleCustomConsoleLogger : IBattleLogger
    {
        private const string Tag = "Battle";

        // ログを1件出力する
        // entry : BattleManagerが内部で組み立てたログエントリ
        public void Log(BattleLogEntry entry)
        {
            if (entry.LogType == BattleLogType.Damage)
                return;

            if (entry.LogType == BattleLogType.ActionBlocked)
                CustomConsoleLog.Warning(Tag, entry.Description);
            else
                CustomConsoleLog.Log(Tag, entry.Description);
        }
    }

    // BattleManager / BattleContext の各種イベントをまとめて購読するヘルパー
    // バトル組み立て時（StartBattle前後）に1度だけ呼び出す
    public static class PPBattleLogBinder
    {
        /* 定数 */

        private const string BattleTag = "Battle";

        /* 初期化・接続 */

        // BattleManager.Logger の差し込みと、ダメージ発生源・キャスト失敗の購読をまとめて行う
        public static void Bind(BattleManager aManager, BattleContext aContext)
        {
            aManager.Logger = new PPBattleCustomConsoleLogger();
            aManager.OnDamageResolved += HandleDamageResolved;
            aContext.OnCastFailed += HandleCastFailed;
        }

        /* 問い合わせ */

        // ダメージ理由を解決できる場合だけ文字列を返す。解決できなければ null
        // スキル由来のダメージは発生元コマンドも持つため、通常攻撃の判定より先にスキルを見る
        private static string ResolveDamageReason(DamageReasonBase aReason) => aReason switch
        {
            AttackDamageReason { Skill: PPSkillDefinition skill } => skill.DisplayName,
            StatusEffectDamageReason { Effect: { } effect } => effect.DisplayName,
            AttackDamageReason { Command: IPPBattleCommand } => "通常攻撃",
            _ => null,
        };

        /* イベントハンドラ */

        // ダメージ確定時に、発生源を解決できればその理由を添えてログを出す
        // aInfo : 確定したダメージ情報
        private static void HandleDamageResolved(DamageInfo aInfo)
        {
            if (aInfo.IsMiss || aInfo.IsNullified || aInfo.Amount <= 0f)
                return;

            string reason = ResolveDamageReason(aInfo.Reason);
            string reasonPart = reason != null ? $" from {reason}" : "";

            CustomConsoleLog.Log(BattleTag,
                $"{aInfo.Target?.DisplayName} took {aInfo.Amount} damage{reasonPart}.");
        }

        // スキル発動失敗時に理由付きでログを出す
        private static void HandleCastFailed(BattleUnit aUnit, BattleSkill aSkill, CastFailReason aReason)
        {
            CustomConsoleLog.Warning(BattleTag,
                $"{aUnit?.DisplayName}の{aSkill?.DisplayName}発動失敗: {aReason}");
        }
    }
}
