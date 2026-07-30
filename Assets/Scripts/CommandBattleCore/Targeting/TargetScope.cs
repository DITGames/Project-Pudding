/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file TargetScope.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief データ定義からターゲットのデフォルトを生成する
 * =====================================*/

namespace CommandBattleCore
{
    // スキル定義がインスペクタ上で対象範囲を指定するための列挙
    // リゾルバのインスタンスは ScriptableObject にシリアライズできないため、
    // データ側はこの enum で持ち、実行時に TargetScopeExtensions.CreateResolver で
    // ITargetResolver へ変換する
    public enum TargetScope
    {
        // 敵単体
        SingleEnemy,
        // 敵全体
        AllEnemies,
        // 味方単体
        SingleAlly,
        // 味方全体
        AllAllies,
        // 敵からランダムに 1 体
        RandomEnemy,
        // 自分自身
        Self
    }

    // TargetScope から対応するリゾルバを生成する拡張メソッド群
    public static class TargetScopeExtensions
    {
        // スコープに対応するリゾルバを新規生成する
        // 単体系は対象未指定の状態で返るため、対象を確定させたい場合は生成後に設定するか、
        // 対象を焼き込んだリゾルバを別途組み立てる
        // aScope : 対象範囲
        // return : 対応するリゾルバ。未知の値は自分自身を対象とする
        public static ITargetResolver CreateResolver(this TargetScope aScope) => aScope switch
        {
            TargetScope.SingleEnemy => new SingleEnemyResolver(),
            TargetScope.AllEnemies => new AllEnemiesResolver(),
            TargetScope.SingleAlly => new SingleAllyResolver(),
            TargetScope.AllAllies => new AllAlliesResolver(),
            TargetScope.RandomEnemy => new RandomEnemyResolver(),
            _ => new SelfResolver(),
        };
    }
}
