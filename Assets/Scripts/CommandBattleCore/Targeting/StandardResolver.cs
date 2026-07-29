/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file StandardResolver.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 標準のターゲットリゾルバ実装
 * =====================================*/
using System.Collections.Generic;

namespace CommandBattleCore
{
    /// <summary>
    /// 敵単体を対象とするリゾルバ。
    /// <para>
    /// 事前に選ばれた対象を保持し、実行時にそれが生存していればそのまま使う。
    /// 既に倒れていた場合は <see cref="IDeadTargetPolicy"/> に代替の決定を委ねる
    /// （コマンドを積んでから実行するまでに対象が倒れうるため）。
    /// </para>
    /// </summary>
    public class SingleEnemyResolver : ITargetResolver
    {
        /// <summary>事前に選ばれた対象。未指定なら常に代替ポリシー任せになる。</summary>
        public BattleUnit SelectedTarget { get; set;  }

        /// <param name="aSelectedTarget">対象として焼き込むユニット。未指定可。</param>
        public SingleEnemyResolver(BattleUnit aSelectedTarget = null) => SelectedTarget = aSelectedTarget;

        /// <summary>
        /// 対象を解決する。選択済みの対象が生存していればそれを、そうでなければ
        /// 敵陣営の生存者を候補として代替ポリシーに決めさせる。
        /// </summary>
        /// <param name="aSource">行動主体のユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>対象ユニットのリスト。</returns>
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext)
        {
            // ターゲットが生存してるならそのまま送る
            if(SelectedTarget is {IsAlive: true})return new List<BattleUnit>{SelectedTarget};

            // 死亡済みの場合は代替ポリシーに委ねる
            var alive = aContext.GetOpponentParty(aSource.Side).GetAliveActiveMembers();
            return aContext.Rules.DeadTargetPolicy.Fallback(aSource, SelectedTarget, alive, aContext);
        }
    }

    /// <summary>
    /// 敵全体を対象とするリゾルバ。生存している敵アクティブメンバーをそのまま返す。
    /// </summary>
    public class AllEnemiesResolver : ITargetResolver
    {
        /// <summary>敵陣営の生存アクティブメンバー全員を返す。</summary>
        /// <param name="aSource">行動主体のユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext)
        => aContext.GetOpponentParty(aSource.Side).GetAliveActiveMembers();
    }

    /// <summary>
    /// 味方単体を対象とするリゾルバ。挙動は <see cref="SingleEnemyResolver"/> と同じで、参照する陣営だけが違う。
    /// </summary>
    public class SingleAllyResolver : ITargetResolver
    {
        /// <summary>事前に選ばれた対象。</summary>
        public BattleUnit SelectedTarget { get; set; }

        /// <param name="aSelectedTarget">対象として焼き込むユニット。未指定可。</param>
        public SingleAllyResolver(BattleUnit aSelectedTarget = null) => SelectedTarget = aSelectedTarget;

        /// <summary>
        /// 対象を解決する。選択済みの対象が生存していればそれを、そうでなければ
        /// 味方陣営の生存者を候補として代替ポリシーに決めさせる。
        /// </summary>
        /// <param name="aSource">行動主体のユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        /// <returns>対象ユニットのリスト。</returns>
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext)
        {
            if(SelectedTarget is {IsAlive: true})return new List<BattleUnit> { SelectedTarget };
            var alive = aContext.GetParty(aSource.Side).GetAliveActiveMembers();
            return aContext.Rules.DeadTargetPolicy.Fallback(aSource, SelectedTarget, alive, aContext);
        }
    }

    /// <summary>
    /// 味方全体を対象とするリゾルバ。生存している味方アクティブメンバーをそのまま返す。
    /// </summary>
    public class AllAlliesResolver : ITargetResolver
    {
        /// <summary>味方陣営の生存アクティブメンバー全員を返す。</summary>
        /// <param name="aSource">行動主体のユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext) =>
            aContext.GetParty(aSource.Side).GetAliveActiveMembers();
    }

    /// <summary>
    /// 敵からランダムに 1 体を対象とするリゾルバ。
    /// 乱数はシード管理・再現性のため <c>aContext.Rules.RandomProvider</c> を経由する。
    /// </summary>
    public class RandomEnemyResolver : ITargetResolver
    {
        /// <summary>敵陣営の生存者から 1 体をランダムに返す。生存者が居なければ空リスト。</summary>
        /// <param name="aSource">行動主体のユニット。</param>
        /// <param name="aContext">バトルコンテキスト。乱数はここから取る。</param>
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext)
        {
            var alive = aContext.GetOpponentParty(aSource.Side).GetAliveActiveMembers();
            return alive.Count > 0
                ? new List<BattleUnit> { alive[aContext.Rules.RandomProvider.NextInt(alive.Count)] }
                : new List<BattleUnit>();
        }
    }

    /// <summary>
    /// 自分自身のみを対象とするリゾルバ。自己バフなどに使う。
    /// </summary>
    public class SelfResolver : ITargetResolver
    {
        /// <summary>行動主体のユニット自身のみを返す。</summary>
        /// <param name="aSource">行動主体のユニット。</param>
        /// <param name="aContext">バトルコンテキスト。</param>
        public List<BattleUnit> Resolve(BattleUnit aSource, BattleContext aContext) => new(){aSource};
    }
}
