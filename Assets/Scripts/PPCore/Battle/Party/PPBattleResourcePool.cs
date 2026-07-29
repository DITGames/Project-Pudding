/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleResourcePool.cs
 * @author hqrse
 * @date 2026/06/21
 * @brief バトルで使用されるリソース定義
 * =====================================*/
using CommandBattleCore;

namespace PPCore
{
    /// <summary>
    /// パーティが保持する属性別の行動リソースプール。
    /// <para>
    /// プッシャー台から落ちたコインが <see cref="PPCoinResourceBridge"/> を経由してここへ加算され、
    /// スキル発動時に <see cref="PPResourceCost"/> の分だけ消費される。
    /// 属性ごとに <see cref="ResourceParameter"/> を 1 本ずつ持ち、
    /// 添字は <see cref="PPTypeAttribute"/> の値をそのまま使う。
    /// </para>
    /// </summary>
    public class PPBattleResourcePool
    {
        /// <summary>属性ごとの行動用リソース。添字は <see cref="PPTypeAttribute"/> と対応する。</summary>
        private readonly ResourceParameter[] mResourcePools;

        /// <summary>
        /// 全属性分のリソースを生成する。
        /// <see cref="ResourceParameter"/> は最大値で初期化されるため、
        /// 生成直後に上限分を Damage して現在値 0 から始まるようにしている。
        /// </summary>
        /// <param name="aMaxPerType">属性 1 種あたりのリソース上限。</param>
        public PPBattleResourcePool(int aMaxPerType)
        {
            mResourcePools = new ResourceParameter[PPTypeAttributeDefinition.TypeCount];
            for (int i = 0; i < mResourcePools.Length; i++)
            {
                var p = new ResourceParameter(aMaxPerType);
                p.Damage(aMaxPerType);  // 初期値0に設定
                mResourcePools[i] = p;
            }
        }

        /// <summary>指定属性のリソースパラメータ本体を取得する。</summary>
        /// <param name="a">対象の属性。</param>
        public ResourceParameter Pool(PPTypeAttribute a) => mResourcePools[(int)a];
        /// <summary>指定属性のリソース現在値を取得する。</summary>
        /// <param name="a">対象の属性。</param>
        public float Current(PPTypeAttribute a) => mResourcePools[(int)a].Current;
        /// <summary>指定属性のリソース最大値を取得する。</summary>
        /// <param name="a">対象の属性。</param>
        public float Max(PPTypeAttribute a) => mResourcePools[(int)a].Max.CurrentValue;
        /// <summary>指定属性のリソースを加算する。上限を超えた分は切り捨てられる。</summary>
        /// <param name="a">対象の属性。</param>
        /// <param name="aAmount">加算量。</param>
        public void Add(PPTypeAttribute a, float aAmount) => mResourcePools[(int)a].Recover(aAmount);

        /// <summary>
        /// コストを支払えるかを実際には消費せずに判定する。
        /// AI の行動候補の絞り込みや UI のグレーアウト判定など、事前チェックに使う。
        /// 浮動小数の誤差で「ちょうど足りている」が不足扱いにならないよう、微小値を足して比較している。
        /// </summary>
        /// <param name="aCost">判定するコスト。null または無コストなら常に true。</param>
        /// <returns>全属性分のリソースが足りていれば true。</returns>
        public bool CanPay(PPResourceCost aCost)
        {
            if(aCost == null || aCost.IsFree)
                return true;
            for (int i = 0; i < PPTypeAttributeDefinition.TypeCount; i++)
            {
                float need = aCost.Get(i);
                if(need > 0f && mResourcePools[i].Current + 0.0001f < need)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// コストを実際に消費する。
        /// 先に <see cref="CanPay"/> で全属性を確認してから減算するため、
        /// 途中で不足して「一部だけ支払われた」状態にはならない。
        /// </summary>
        /// <param name="aCost">支払うコスト。null または無コストなら何もせず true。</param>
        /// <returns>支払えた場合 true。不足していれば何も消費せず false。</returns>
        public bool TryPay(PPResourceCost aCost)
        {
            if(aCost == null || aCost.IsFree)
                return true;
            if(!CanPay(aCost))
                return false;
            for (int i = 0; i < PPTypeAttributeDefinition.TypeCount; i++)
            {
                float need = aCost.Get(i);
                if (need > 0f)
                {
                    mResourcePools[i].Damage(need);
                }
            }
            return true;
        }
    }
}
