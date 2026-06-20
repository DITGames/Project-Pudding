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
    public enum TargetScope
    {
        SingleEnemy, AllEnemies, SingleAlly, AllAllies, RandomEnemy, Self
    }

    public static class TargetScopeExtensions
    {
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