/* =====================================
 * Copyright DITGames. All rights reserved.
 * @file PPBattleUnitSelectMenuView.cs
 * @author DITGames
 * @date 2026/09/07
 * @brief 自陣ユニット選択メニュー（入力モード中のみ表示する専用ボタン列）
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // 「誰が行動するか」を選ばせる専用メニュー
    // 盤面表示（PPBattleUnitView）とは独立しており、入力モードに入ったときだけ生成・表示し、
    // ユニットが決まったら閉じる。盤面側は表示専任のままにできるため、
    // 将来盤面をワールド空間（HD2D）表示へ差し替えても本メニューは影響を受けない
    public class PPBattleUnitSelectMenuView : MonoBehaviour
    {
        [Label("ユニットボタンプレハブ")]
        [SerializeField] private PPBattleCommandButton mButtonPrefab;
        [Label("ユニットボタン表示領域")]
        [SerializeField] private RectTransform mContent;
        [Label("ビジュアルカタログ")]
        [SerializeField] private PPUnitVisualCatalog mIconCatalog;

        // ユニットが選択されたときに発火する
        public event Action<BattleUnit> OnUnitSelected;

        // 生成済みのユニットボタン。閉じるときにまとめて破棄する
        private readonly List<PPBattleCommandButton> mUnitButtons = new();

        // 候補ユニット分のボタンを生成して表示する
        // aUnits : 選択候補のユニット
        public void Show(IEnumerable<BattleUnit> aUnits)
        {
            if (mContent == null)
            {
                Debug.LogWarning("mContent is null");
                return;
            }

            Clear();
            gameObject.SetActive(true);

            PPBattleCommandButton firstBtn = null;
            foreach (var unit in aUnits)
            {
                var btn = Instantiate(mButtonPrefab, mContent);
                // カタログ未設定、または該当アイコン未登録のどちらもアイコンなしとして扱う
                var icon = mIconCatalog != null
                    ? mIconCatalog.Resolve(unit.UnitId)?.UnitIcon
                    : null;
                btn.Setup(icon, null, () => OnUnitSelected?.Invoke(unit));
                mUnitButtons.Add(btn);

                // 初期フォーカス設定
                firstBtn ??= btn;
            }

            // 直後にフォーカスを当てるため、レイアウトの反映を次フレームまで待たない
            LayoutRebuilder.ForceRebuildLayoutImmediate(mContent);

            if (firstBtn != null)
            {
                EventSystem.current.SetSelectedGameObject(firstBtn.FocusTarget);
            }
        }

        // メニューを閉じ、生成したボタンを破棄する
        public void Hide()
        {
            Clear();
            gameObject.SetActive(false);
        }

        // 生成済みのユニットボタンをすべて破棄する
        // 表示のたびに作り直すため、開く前と閉じるときの両方から呼ばれる
        private void Clear()
        {
            foreach (var btn in mUnitButtons)
            {
                if (btn == null) continue;
                Destroy(btn.gameObject);
            }
            mUnitButtons.Clear();
        }
    }
}
