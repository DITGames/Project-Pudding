/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleBase.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief 拠点の設定・道の上の拠点・拠点の破壊による勝敗判定
 * =====================================*/

using System;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // 拠点 1 つ分の設定。ステージの定義と、単体で Play するときのランナーが持つ
    [Serializable]
    public class PPBaseSettings
    {
        /* インスペクタ設定 */

        [Label("表示名")]
        [SerializeField] private string mDisplayName = "拠点";
        [Label("HP")]
        [Min(1f)]
        [SerializeField] private float mMaxHp = 20000f;
        // ユニットと同じダメージ計算で、受けるダメージを減らす
        [Label("まもり")]
        [Min(0f)]
        [SerializeField] private float mDefense = 0f;
        [Label("属性")]
        [SerializeField] private PPTypeAttribute mTypeAttribute = PPTypeAttribute.Normal;

        /* プロパティ */

        public string DisplayName => mDisplayName;
        public float MaxHp => mMaxHp;
        public float Defense => mDefense;
        public PPTypeAttribute TypeAttribute => mTypeAttribute;

        /* 生成 */

        public PPBaseSettings() { }

        public PPBaseSettings(string aDisplayName, float aMaxHp, float aDefense = 0f, PPTypeAttribute aTypeAttribute = PPTypeAttribute.Normal)
        {
            mDisplayName = aDisplayName;
            mMaxHp = aMaxHp;
            mDefense = aDefense;
            mTypeAttribute = aTypeAttribute;
        }

        /* 操作 */

        // 拠点をユニットとして作る。ユニットと同じダメージ計算を通すため、攻撃・すばやさ・きようさは 0 にする
        public BattleUnit CreateRuntimeUnit(string aUnitId)
        {
            var parameters = new ParameterSet(Mathf.Max(1f, mMaxHp), 0f, mDefense, 0f);
            return new PPBattleUnit(aUnitId, mDisplayName, parameters, new PPParameterSet(0f), mTypeAttribute);
        }
    }

    // 道の端にある拠点。ユニットとして HP を持ち、相手のユニットの攻撃を受ける
    public class PPFieldBase
    {
        /* プロパティ */

        public BattleUnit Unit { get; }
        public BattleSide Side { get; }
        // 味方拠点は道の左端(0)、敵拠点は右端(道の長さ)
        public float Position { get; }
        // 直前に知らせた HP の割合。HP の割合が変わったことを知らせるときの「変わる前」に使う
        public float LastHpRatio { get; set; } = 1f;
        public bool IsAlive => Unit.IsAlive;

        /* static 関数 */

        // HP の割合が aBefore から aAfter へ変わる間に、aThreshold(0〜1)を下回ったか。ウェーブの「HP が x% になった」の判定に使う
        public static bool HasCrossed(float aBefore, float aAfter, float aThreshold) => aBefore > aThreshold && aAfter <= aThreshold;

        /* 生成 */

        public PPFieldBase(BattleUnit aUnit, BattleSide aSide, float aPosition)
        {
            Unit = aUnit;
            Side = aSide;
            Position = aPosition;
        }
    }

    // 拠点の破壊で勝敗を決める。敵拠点を壊せば勝ち、味方拠点が壊れたら負け。道に拠点が無いバトルは全滅で判定する
    public class PPBaseResultChecker : DefaultBattleResultChecker
    {
        /* 評価・判定 */

        public override BattleResult CheckResult(BattleContext aContext)
        {
            var allyBase = aContext.Field?.GetBase(BattleSide.Ally);
            var enemyBase = aContext.Field?.GetBase(BattleSide.Enemy);
            if (allyBase == null || enemyBase == null) return base.CheckResult(aContext);
            if (aContext.EscapeRequested) return new BattleResult(BattleResultType.Escaped);

            if (!allyBase.IsAlive && !enemyBase.IsAlive) return new BattleResult(BattleResultType.Draw);
            if (!enemyBase.IsAlive) return new BattleResult(BattleResultType.Victory);
            if (!allyBase.IsAlive) return new BattleResult(BattleResultType.Defeat);
            return BattleResult.InProgress;
        }
    }
}
