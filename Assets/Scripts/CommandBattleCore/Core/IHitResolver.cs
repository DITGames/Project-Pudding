/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file IHitResolver.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief 命中・クリティカルなどの拡張
 * =====================================*/

namespace CommandBattleCore
{
    // 命中判定とクリティカル判定の結果をまとめた構造体。BattleContext.ResolveHit の戻り値
    public struct HitInfo
    {
        // 命中したか外れたか
        public HitResult mResult;
        // クリティカル判定の結果。命中した場合のみ評価される
        public CriticalInfo mCriticalInfo;
    }

    // 命中判定の結果
    public enum HitResult
    {
        Hit,
        Miss,
    }

    // クリティカル判定の結果。発生有無と、発生時に掛けるダメージ倍率を持つ
    public struct CriticalInfo
    {
        // クリティカルが発生したか
        public bool IsCritical;
        // クリティカル時のダメージ倍率
        public float CriticalMultiplier;
    }

    // 命中判定を行うリゾルバ。BattleRules.HitResolver に差し込む
    // 命中率の計算式をゲームごとに変えられるよう分離してある
    public interface IHitResolver
    {
        // 命中判定を行う
        // aSource : 攻撃側ユニット
        // aTarget : 防御側ユニット
        // aInfo : 判定対象のダメージ情報
        // aContext : バトルコンテキスト。乱数はここから取る
        // return : 命中判定の結果
        HitResult Resolve(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext);
    }

    // 必中のリゾルバ。命中率を考えないバトルやデバッグ時に差し込む
    public class DefaultHitResolver : IHitResolver
    {
        // 常に命中を返す
        public HitResult Resolve(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext)
            => HitResult.Hit;
    }

    // 標準の命中リゾルバ。攻守のパラメータを見ず、固定 95% で判定する暫定実装
    // 回避率などを反映させる場合はこのクラスを差し替える
    public class StandardHitResolver : IHitResolver
    {
        // 固定確率で命中判定を行う
        public HitResult Resolve(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext)
        {
            float hitChance = 0.95f;
            // 攻撃側の試行なので、攻撃側の乱数列から引く
            if (aSource.ResolveRandom(aContext).NextFloat() > hitChance) return HitResult.Miss;
            else return HitResult.Hit;
        }
    }

    // クリティカル判定を行うリゾルバ。BattleRules.CriticalResolver に差し込む
    public interface ICriticalResolver
    {
        // クリティカル判定を行う。命中した場合にのみ呼ばれる
        // aSource : 攻撃側ユニット
        // aTarget : 防御側ユニット
        // aInfo : 判定対象のダメージ情報
        // aContext : バトルコンテキスト。乱数はここから取る
        // return : クリティカルの発生有無と倍率
        CriticalInfo Resolve(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext);
    }

    // 標準のクリティカルリゾルバ。発生率 10%・倍率 1.2 倍の固定値で判定する暫定実装
    // 発生率の求め方だけを変えたい場合は、派生して ResolveCriticalChance をオーバーライドする
    public class StandardCriticalResolver : ICriticalResolver
    {
        // 既定の発生率（0～1）
        public const float DefaultCriticalChance = 0.1f;
        // 既定のクリティカル倍率
        public const float DefaultCriticalMultiplier = 1.2f;

        // 発生率に応じてクリティカル判定を行う。倍率は発生有無に関わらず設定される
        public virtual CriticalInfo Resolve(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext)
        {
            CriticalInfo info = new CriticalInfo();
            info.IsCritical = false;
            info.CriticalMultiplier = DefaultCriticalMultiplier;
            float criticalChance = ResolveCriticalChance(aSource, aTarget, aInfo, aContext);
            // 攻撃側の試行なので、攻撃側の乱数列から引く
            // 発生率 1 以上は必ず発生させる（float 変換で乱数が 1.0 に丸まる場合があるため）
            // 乱数列の消費数を発生率によって変えないよう、判定前に必ず 1 回引いておく
            float roll = aSource.ResolveRandom(aContext).NextFloat();
            if (criticalChance >= 1f || roll < criticalChance) info.IsCritical = true;
            return info;
        }

        // クリティカル発生率（0～1）を求める。既定は固定値
        // 1 以上を返せば必ず発生し、0 以下なら発生しない
        // aSource : 攻撃側ユニット
        // aTarget : 防御側ユニット
        // aInfo : 判定対象のダメージ情報
        // aContext : バトルコンテキスト
        // return : 発生率
        protected virtual float ResolveCriticalChance(BattleUnit aSource, BattleUnit aTarget, DamageInfo aInfo, BattleContext aContext)
            => DefaultCriticalChance;
    }
}
