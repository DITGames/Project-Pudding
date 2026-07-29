/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file TargetScope.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief データ定義からターゲットのデフォルトを生成する
 * =====================================*/
using UnityEngine;

namespace CommandBattleCore
{
    /// <summary>
    /// スキル定義がインスペクタ上で対象範囲を指定するための列挙。
    /// リゾルバのインスタンスは ScriptableObject にシリアライズできないため、
    /// データ側はこの enum で持ち、実行時に <see cref="TargetScopeExtensions.CreateResolver"/> で
    /// <see cref="ITargetResolver"/> へ変換する。
    /// </summary>
    public enum TargetScope
    {
        /// <summary>敵単体。</summary>
        SingleEnemy,
        /// <summary>敵全体。</summary>
        AllEnemies,
        /// <summary>味方単体。</summary>
        SingleAlly,
        /// <summary>味方全体。</summary>
        AllAllies,
        /// <summary>敵からランダムに 1 体。</summary>
        RandomEnemy,
        /// <summary>自分自身。</summary>
        Self
    }

    /// <summary>
    /// <see cref="TargetScope"/> から対応するリゾルバを生成する拡張メソッド群。
    /// </summary>
    public static class TargetScopeExtensions
    {
        /// <summary>
        /// スコープに対応するリゾルバを新規生成する。
        /// 単体系は対象未指定の状態で返るため、対象を確定させたい場合は生成後に設定するか、
        /// 対象を焼き込んだリゾルバを別途組み立てる。
        /// </summary>
        /// <param name="aScope">対象範囲。</param>
        /// <returns>対応するリゾルバ。未知の値は自分自身を対象とする。</returns>
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
