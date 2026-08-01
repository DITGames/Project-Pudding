/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleLogBinder.cs
 * @author hqrse
 * @date 2026/08/02
 * @brief BattleManager/BattleContextのイベントをCustomConsoleへ統合するロガー
 * =====================================*/

using CommandBattleCore;
using CustomConsole;

namespace PPCore
{
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
        private const string BattleTag = "Battle";

        // BattleManager.Logger の差し込みと、ダメージ発生源・キャスト失敗の購読をまとめて行う
        // aManager : ログを紐付けるバトルマネージャ
        // aContext : スキル発動失敗イベント(OnCastFailed)を持つバトルコンテキスト
        public static void Bind(BattleManager aManager, BattleContext aContext)
        {
            aManager.Logger = new PPBattleCustomConsoleLogger();
            aManager.OnDamageResolved += HandleDamageResolved;
            aContext.OnCastFailed += HandleCastFailed;
        }

        // ダメージ確定時に、発生源を解決できればその理由を添えてログを出す
        // aInfo : 確定したダメージ情報
        private static void HandleDamageResolved(DamageInfo aInfo)
        {
            if (aInfo.IsMiss || aInfo.IsNullified || aInfo.Amount <= 0f)
                return;

            string reason = ResolveDamageReason(aInfo.SourceAbility);
            string reasonPart = reason != null ? $" from {reason}" : "";

            CustomConsoleLog.Log(BattleTag,
                $"{aInfo.Target?.DisplayName} took {aInfo.Amount} damage{reasonPart}.");
        }

        // ダメージ理由を解決できる場合だけ文字列を返す。解決できなければ null
        // aSourceAbility : DamageInfo.SourceAbility に格納された発生源オブジェクト
        private static string ResolveDamageReason(object aSourceAbility) => aSourceAbility switch
        {
            PPSkillDefinition skill => skill.DisplayName,
            StatusEffect effect => effect.DisplayName,
            IPPBattleCommand => "通常攻撃",
            _ => null,
        };

        // スキル発動失敗時に理由付きでログを出す
        // aUnit : 発動しようとしたユニット
        // aSkill : 発動しようとしたスキル
        // aReason : 失敗理由
        private static void HandleCastFailed(BattleUnit aUnit, BattleSkill aSkill, CastFailReason aReason)
        {
            CustomConsoleLog.Warning(BattleTag,
                $"{aUnit?.DisplayName}の{aSkill?.DisplayName}発動失敗: {aReason}");
        }
    }
}
