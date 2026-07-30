/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IHitResolver.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 命中・クリティカルなどの拡張
 * =====================================*/

namespace CommandBattleCore
{
    /// <summary>
    /// 命中判定とクリティカル判定の結果をまとめた構造体。
    /// <see cref="BattleContext.ResolveHit"/> の戻り値。
    /// </summary>
    public struct HitInfo
    {
        /// <summary>命中したか外れたか。</summary>
        public HitResult mResult;
        /// <summary>クリティカル判定の結果。命中した場合のみ評価される。</summary>
        public CriticalInfo mCriticalInfo;
    }

    /// <summary>命中判定の結果。</summary>
    public enum HitResult
    {
        /// <summary>命中。</summary>
        Hit,
        /// <summary>外れ。</summary>
        Miss,
    }

    /// <summary>
    /// クリティカル判定の結果。発生有無と、発生時に掛けるダメージ倍率を持つ。
    /// </summary>
    public struct CriticalInfo
    {
        /// <summary>クリティカルが発生したか。</summary>
        public bool IsCritical;
        /// <summary>クリティカル時のダメージ倍率。</summary>
        public float CriticalMultiplier;
    }

    /// <summary>
    /// 命中判定を行うリゾルバ。<see cref="BattleRules.HitResolver"/> に差し込む。
    /// 命中率の計算式をゲームごとに変えられるよう分離してある。
    /// </summary>
    public interface IHitResolver
    {
        /// <summary>
        /// 命中判定を行う。
        /// </summary>
        /// <param name="aSource">攻撃側ユニット。</param>
        /// <param name="aTarget">防御側ユニット。</param>
        /// <param name="aInfo">判定対象のダメージ情報。</param>
        /// <param name="aContext">バトルコンテキスト。乱数はここから取る。</param>
        /// <returns>命中判定の結果。</returns>
        HitResult Resolve(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext);
    }

    /// <summary>
    /// 必中のリゾルバ。命中率を考えないバトルやデバッグ時に差し込む。
    /// </summary>
    public class DefaultHitResolver : IHitResolver
    {
        /// <summary>常に命中を返す。</summary>
        public HitResult Resolve(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext)
            => HitResult.Hit;
    }

    /// <summary>
    /// 標準の命中リゾルバ。攻守のパラメータを見ず、固定 95% で判定する暫定実装。
    /// 回避率などを反映させる場合はこのクラスを差し替える。
    /// </summary>
    public class StandardHitResolver : IHitResolver
    {
        /// <summary>固定確率で命中判定を行う。</summary>
        public HitResult Resolve(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext)
        {
            float hitChance = 0.95f;
            if (aContext.Rules.RandomProvider.NextFloat() > hitChance) return HitResult.Miss;
            else return HitResult.Hit;
        }
    }

    /// <summary>
    /// クリティカル判定を行うリゾルバ。<see cref="BattleRules.CriticalResolver"/> に差し込む。
    /// </summary>
    public interface ICriticalResolver
    {
        /// <summary>
        /// クリティカル判定を行う。命中した場合にのみ呼ばれる。
        /// </summary>
        /// <param name="aSource">攻撃側ユニット。</param>
        /// <param name="aTarget">防御側ユニット。</param>
        /// <param name="aInfo">判定対象のダメージ情報。</param>
        /// <param name="aContext">バトルコンテキスト。乱数はここから取る。</param>
        /// <returns>クリティカルの発生有無と倍率。</returns>
        CriticalInfo Resolve(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext);
    }

    /// <summary>
    /// 標準のクリティカルリゾルバ。発生率 10%・倍率 1.2 倍の固定値で判定する暫定実装。
    /// </summary>
    public class StandardCriticalResolver : ICriticalResolver
    {
        /// <summary>固定確率でクリティカル判定を行う。倍率は発生有無に関わらず設定される。</summary>
        public CriticalInfo Resolve(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext)
        {
            CriticalInfo info = new CriticalInfo();
            info.IsCritical = false;
            info.CriticalMultiplier = 1.2f;
            float criticalChance = 0.1f;
            if (aContext.Rules.RandomProvider.NextFloat() < criticalChance) info.IsCritical = true;
            return info;
        }
    }
}
