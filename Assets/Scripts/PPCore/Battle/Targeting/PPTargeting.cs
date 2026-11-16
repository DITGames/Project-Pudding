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
    // リゾルバのインスタンスは ScriptableObject にシリアライズできないため、
    // データ側はこの enum で持ち、実行時に TargetScopeExtensions.CreateResolver で
    // ITargetResolver へ変換する
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

    // 行動の対象を決めるリゾルバ
    // 「敵単体」「味方全体」といった対象の取り方をこのインターフェースの実装として表現し、
    // コマンドやスキルはリゾルバを差し替えるだけで対象範囲を変えられる
    // 実装は StandardResolver 系を参照
    // 呼び出し側は直接ではなく BattleContext.ResolveTargets 経由で使うこと
    // そちらを通すと ITargetFilter による絞り込みも掛かる
    public interface ITargetResolver
    {
        // 対象を解決する
        // aSource : 行動主体のユニット。陣営の判定に使う
        // aContext : バトルコンテキスト
        // return : 対象ユニットのリスト。該当なしの場合は空リスト
        List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext);
    }

    // 敵単体を対象とするリゾルバ
    // 事前に選ばれた対象を保持し、実行時にそれが生存していればそのまま使う
    // 既に倒れていた場合は IDeadTargetPolicy に代替の決定を委ねる
    // （コマンドを積んでから実行するまでに対象が倒れうるため）
    public class SingleEnemyResolver : ITargetResolver
    {
        // 事前に選ばれた対象。未指定なら常に代替ポリシー任せになる
        public BattleUnit SelectedTarget { get; set;  }

        // aSelectedTarget : 対象として焼き込むユニット。未指定可
        public SingleEnemyResolver(BattleUnit aSelectedTarget = null) => SelectedTarget = aSelectedTarget;

        // 対象を解決する。選択済みの対象が生存していればそれを、そうでなければ
        // 敵陣営の生存者を候補として代替ポリシーに決めさせる
        // aSource : 行動主体のユニット
        // aContext : バトルコンテキスト
        // return : 対象ユニットのリスト
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
        // aSource : 行動主体のユニット
        // aContext : バトルコンテキスト
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
        // aSource : 行動主体のユニット
        // aContext : バトルコンテキスト
        // return : 対象ユニットのリスト
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
        // aSource : 行動主体のユニット
        // aContext : バトルコンテキスト
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext) =>
            aContext.GetParty(aSource.Side).GetAliveActiveMembers();
    }

    // 敵からランダムに 1 体を対象とするリゾルバ
    // 乱数はシード管理・再現性のため、行動するユニット自身の供給元を経由する
    public class RandomEnemyResolver : ITargetResolver
    {
        // 敵陣営の生存者から 1 体をランダムに返す。生存者が居なければ空リスト
        // aSource : 行動主体のユニット
        // aContext : バトルコンテキスト。乱数はここから取る
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
        // aSource : 行動主体のユニット
        // aContext : バトルコンテキスト
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext) => new(){aSource};
    }

    // ITargetResolver が出した対象候補を後段で絞り込む・並べ替えるフィルタ
    // BattleRules.TargetFilters に登録された順に適用され、
    // 前段の出力が次段の入力になる
    // 「かばう」「挑発でヘイト先を強制する」といった、
    // 対象の取り方そのものではなく後から差し替わる仕様をここで表現する
    public interface ITargetFilter
    {
        // 対象候補を加工する
        // aSource : 行動主体のユニット
        // aCandidates : 前段までの対象候補
        // aContext : バトルコンテキスト
        // return : 加工後の対象リスト
        List<BattleUnit> Filter(BattleUnit aSource, List<BattleUnit> aCandidates, BattleContext aContext);
    }

    // 単体対象を指定していたが、行動時点でその対象が戦闘不能だった場合の代替決定ポリシー
    // コマンド入力から実行までにラグがある（キューに積まれる・演出を挟む）以上、
    // 選んだ相手が先に倒れているケースは必ず起きる。そのときに
    // 「別の相手に流す」のか「不発にする」のかはゲーム性の問題なので、差し替え可能にしてある
    // BattleRules.DeadTargetPolicy に設定する
    public interface IDeadTargetPolicy
    {
        // 代替対象を決定する
        // aSource : 行動主体のユニット
        // aNoneTarget : 本来狙っていた、既に対象にできないユニット
        // aAliveCandidates : 代替候補となる生存ユニット
        // aContext : バトルコンテキスト
        // return : 代替対象のリスト。不発にする場合は空リスト
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
        // aScope : スキルのターゲット範囲
        public static bool NeedsManualTarget(TargetScope aScope)
            => aScope is TargetScope.SingleEnemy or TargetScope.SingleAlly;

        // 対象候補が味方側かを判定する。対象選択で敵味方どちらを選択可能にするかの判断に使う
        // aScope : スキルのターゲット範囲
        public static bool IsAllySide(TargetScope aScope)
            => aScope is TargetScope.SingleAlly or TargetScope.AllAllies or TargetScope.Self;
    }
}
