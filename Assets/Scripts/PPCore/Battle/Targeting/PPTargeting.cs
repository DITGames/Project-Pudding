/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPTargeting.cs
 * @author hqrse
 * @date 2026/07/02
 * @brief ターゲット解決（対象範囲・リゾルバ・フィルタ・死亡対象の代替）
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;

namespace PPCore
{
    // スキル定義がインスペクタ上で対象範囲を指定するための列挙
    // 実行時に TargetScopeExtensions.CreateResolver で ITargetResolver へ変換する
    public enum TargetScope
    {
        [InspectorName("敵単体")]
        SingleEnemy,
        [InspectorName("敵全体")]
        AllEnemies,
        [InspectorName("味方単体")]
        SingleAlly,
        [InspectorName("味方全体")]
        AllAllies,
        [InspectorName("敵ランダム")]
        RandomEnemy,
        [InspectorName("自分")]
        Self
    }

    // TargetScope から対応するリゾルバを生成する拡張メソッド群
    public static class TargetScopeExtensions
    {
        // スコープに対応するリゾルバを新規生成する
        // 単体系は対象未指定の状態で返るため、対象は生成後に設定する
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

    // 行動の対象を決めるリゾルバ。「敵単体」「味方全体」といった対象の取り方を実装として表現する
    // 呼び出し側は BattleContext.ResolveTargets 経由で使うこと（ITargetFilter による絞り込みも掛かる）
    public interface ITargetResolver
    {
        // 対象を解決する
        List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext);
    }

    // 敵単体を対象とするリゾルバ。事前に選ばれた対象が生存していればそのまま使う
    // 既に倒れていた場合は IDeadTargetPolicy に代替の決定を委ねる
    public class SingleEnemyResolver : ITargetResolver
    {
        // 事前に選ばれた対象。未指定なら常に代替ポリシー任せになる
        public BattleUnit SelectedTarget { get; set;  }

        // aSelectedTarget : 対象として焼き込むユニット。未指定可
        public SingleEnemyResolver(BattleUnit aSelectedTarget = null) => SelectedTarget = aSelectedTarget;

        // 対象を解決する。選択済みの対象が生存していればそれを、そうでなければ
        // 敵陣営の生存者を候補として代替ポリシーに決めさせる
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext)
        {
            // ターゲットが生存してるならそのまま送る
            if(SelectedTarget is {IsAlive: true})return new List<BattleUnit>{SelectedTarget};

            // 死亡済みの場合は代替ポリシーに委ねる
            var alive = aContext.GetOpponentParty(aSource.Side).GetAliveActiveMembers();
            return aContext.Rules.DeadTargetPolicy.Fallback(aSource, SelectedTarget, alive, aContext);
        }
    }

    // 敵全体を対象とするリゾルバ。生存している敵アクティブメンバーをそのまま返す
    public class AllEnemiesResolver : ITargetResolver
    {
        // 敵陣営の生存アクティブメンバー全員を返す
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext)
        => aContext.GetOpponentParty(aSource.Side).GetAliveActiveMembers();
    }

    // 味方単体を対象とするリゾルバ。挙動は SingleEnemyResolver と同じで、参照する陣営だけが違う
    public class SingleAllyResolver : ITargetResolver
    {
        // 事前に選ばれた対象
        public BattleUnit SelectedTarget { get; set; }

        // aSelectedTarget : 対象として焼き込むユニット。未指定可
        public SingleAllyResolver(BattleUnit aSelectedTarget = null) => SelectedTarget = aSelectedTarget;

        // 対象を解決する。選択済みの対象が生存していればそれを、そうでなければ
        // 味方陣営の生存者を候補として代替ポリシーに決めさせる
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext)
        {
            if(SelectedTarget is {IsAlive: true})return new List<BattleUnit> { SelectedTarget };
            var alive = aContext.GetParty(aSource.Side).GetAliveActiveMembers();
            return aContext.Rules.DeadTargetPolicy.Fallback(aSource, SelectedTarget, alive, aContext);
        }
    }

    // 味方全体を対象とするリゾルバ。生存している味方アクティブメンバーをそのまま返す
    public class AllAlliesResolver : ITargetResolver
    {
        // 味方陣営の生存アクティブメンバー全員を返す
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext) =>
            aContext.GetParty(aSource.Side).GetAliveActiveMembers();
    }

    // 敵からランダムに 1 体を対象とするリゾルバ
    // 乱数はシード管理・再現性のため、行動するユニット自身の供給元を経由する
    public class RandomEnemyResolver : ITargetResolver
    {
        // 敵陣営の生存者から 1 体をランダムに返す。生存者が居なければ空リスト
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext)
        {
            var alive = aContext.GetOpponentParty(aSource.Side).GetAliveActiveMembers();
            return alive.Count > 0
                ? new List<BattleUnit> { alive[aSource.ResolveRandom(aContext).NextInt(alive.Count)] }
                : new List<BattleUnit>();
        }
    }

    // 自分自身のみを対象とするリゾルバ。自己バフなどに使う
    public class SelfResolver : ITargetResolver
    {
        // 行動主体のユニット自身のみを返す
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext) => new(){aSource};
    }

    // ITargetResolver が出した対象候補を後段で絞り込む・並べ替えるフィルタ（「かばう」「挑発」など）
    // BattleRules.TargetFilters に登録された順に適用される
    public interface ITargetFilter
    {
        // 対象候補を加工する
        List<BattleUnit> Filter(BattleUnit aSource, List<BattleUnit> aCandidates, BattleContext aContext);
    }

    // 単体対象が行動時点で戦闘不能だった場合の代替決定ポリシー（別の相手に流すか、不発にするか）
    // BattleRules.DeadTargetPolicy に設定する
    public interface IDeadTargetPolicy
    {
        // 代替対象を決定する
        List<BattleUnit> Fallback(BattleUnit aSource, BattleUnit aNoneTarget,
            List<BattleUnit> aAliveCandidates, BattleContext aContext);
    }

    // 生存している先頭のユニットへ対象を置き換えるポリシー。既定の挙動
    // 行動が無駄になりにくい代わりに、狙いが必ずずれる
    public class FirstAliveFallback : IDeadTargetPolicy
    {
        // 生存候補の先頭を返す。候補が無ければ空リスト
        public List<BattleUnit> Fallback(BattleUnit aSource, BattleUnit aNoneTarget,
            List<BattleUnit> aAliveCandidates, BattleContext aContext)
                => aAliveCandidates.Count > 0 ? new List<BattleUnit> {aAliveCandidates[0]} : new List<BattleUnit>();
    }

    // 代替を探さず不発にするポリシー。狙った相手が倒れていたら行動が空振りする
    public class NoFallback : IDeadTargetPolicy
    {
        // 常に空リストを返す
        public List<BattleUnit> Fallback(BattleUnit aSource, BattleUnit aNoneTarget,
            List<BattleUnit> aAliveCandidates, BattleContext aContext)
            => new List<BattleUnit>();
    }

    // 生存ユニットからランダムに代替を選ぶポリシー
    public class RandomFallback : IDeadTargetPolicy
    {
        // 生存候補からランダムに 1 体返す。候補が無ければ空リスト
        public List<BattleUnit> Fallback(BattleUnit aSource, BattleUnit aNoneTarget,
            List<BattleUnit> aAliveCandidates, BattleContext aContext)
                => aAliveCandidates.Count > 0
                    ? new List<BattleUnit> {aAliveCandidates[aSource.ResolveRandom(aContext).NextInt(aAliveCandidates.Count)]}
                    : new List<BattleUnit>();
    }

    // ターゲット範囲に関する判定を集約したユーティリティ
    // 入力フローの分岐（対象選択に進むか即確定か、候補は味方か敵か）で使う
    public static class PPTargeting
    {
        // プレイヤーに対象を選ばせる必要があるかを判定する
        // 単体対象のみ true で、全体・自己完結の行動は選択を挟まず即確定できる
        public static bool NeedsManualTarget(TargetScope aScope)
            => aScope is TargetScope.SingleEnemy or TargetScope.SingleAlly;

        // 対象候補が味方側かを判定する。対象選択で敵味方どちらを選択可能にするかの判断に使う
        // aScope : スキルのターゲット範囲
        public static bool IsAllySide(TargetScope aScope)
            => aScope is TargetScope.SingleAlly or TargetScope.AllAllies or TargetScope.Self;
    }
}
