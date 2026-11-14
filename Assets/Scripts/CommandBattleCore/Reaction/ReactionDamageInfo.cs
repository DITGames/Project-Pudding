/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file ReactionDamageInfo.cs
 * @author hqrse
 * @date 2026/11/09
 * @brief リアクション（反撃など）として発生したことを表す情報
 * =====================================*/

namespace CommandBattleCore
{
    // リアクション（反撃・とげなど）として発生した行動・ダメージに付く情報
    // BattleManager.DispatchReactions がリアクションのコマンドに設定し、
    // そのコマンドの実行中に発生したダメージへ BattleUnit.ApplyDamage が引き継ぐ
    // 「何によるダメージか」（DamageInfo.Reason）とは独立した軸として扱う
    public class ReactionDamageInfo
    {
        // 連鎖の深さ。1 = 通常の行動へのリアクション、2 = リアクションへのリアクション …
        public int Depth { get; }
        // 発生元のリアクション
        public IBattleReaction Reaction { get; }

        // aDepth : 連鎖の深さ。1 未満は 1 に丸める
        // aReaction : 発生元のリアクション
        public ReactionDamageInfo(int aDepth, IBattleReaction aReaction)
        {
            Depth = aDepth < 1 ? 1 : aDepth;
            Reaction = aReaction;
        }
    }
}
