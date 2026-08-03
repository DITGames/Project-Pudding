/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file IPPPartySaveSource.cs
 * @author hqrse
 * @date 2026/08/02
 * @brief セーブデータからパーティを生成するための抽象
 * =====================================*/

using System.Collections.Generic;

namespace PPCore
{
    // 本番用のユニット所持データ（セーブデータ）からパーティを生成するための抽象
    // 実装（PPBattleSaveData 等）は別タスクで用意する
    // PPPartyFactory が依存する最小限の形として、所持ユニットとレベルの一覧だけを定義する
    public interface IPPPartySaveSource
    {
        // 所持しているユニットとそのレベルの一覧を返す
        // return : 所持ユニットの一覧
        IReadOnlyList<PPPartySaveUnitEntry> GetOwnedUnits();
    }

    // セーブデータ側が返す、ユニット 1 体分の所持情報
    public readonly struct PPPartySaveUnitEntry
    {
        public readonly PPUnitDefinition Unit;
        public readonly int Level;

        // aUnit : 所持しているユニットの定義
        // aLevel : そのユニットの現在レベル
        public PPPartySaveUnitEntry(PPUnitDefinition aUnit, int aLevel)
        {
            Unit = aUnit;
            Level = aLevel;
        }
    }
}
