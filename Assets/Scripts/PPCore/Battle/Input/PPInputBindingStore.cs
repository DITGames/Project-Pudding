/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPInputBindingStore.cs
 * @author hqrse
 * @date 2026/11/18
 * @brief キー割り当ての変更・保存・読み込み（変更できる操作の定義・割り当ての窓口）
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using CustomConsole;

namespace PPCore
{
    // キー設定の操作の分類。同じキーを使えないのは同じ分類の中だけで、分類が違えば同じキーを割り当てられる
    public enum PPRebindCategory
    {
        // メニューなどの画面の操作
        UI = 0,
        // バトル中のプッシャーなどの操作
        Game = 1,
    }

    // キー設定で変更できる操作 1 つ分。入力アクションの名前・画面に出す名前・分類を持つ
    public readonly struct PPRebindableAction
    {
        /* 状態 */

        public readonly string ActionName;
        public readonly string DisplayName;
        public readonly PPRebindCategory Category;

        /* 生成 */

        public PPRebindableAction(string aActionName, string aDisplayName, PPRebindCategory aCategory)
        {
            ActionName = aActionName;
            DisplayName = aDisplayName;
            Category = aCategory;
        }
    }

    // キー割り当ての窓口。各操作はキーボード 2 枠・ゲームパッド 2 枠を持ち、既定は PPRuntimeSettings、変更は PlayerPrefs に置く
    // 左右・上下の操作とキー設定を開く操作は変更できない
    public static class PPInputBindingStore
    {
        /* 内部型 */

        // 保存する枠 1 つ分
        [Serializable]
        private class SavedBinding
        {
            public string Action;
            public bool IsGamepad;
            public int Slot;
            public string Path;
        }

        // 保存する枠の一覧。JsonUtility は配列を直接扱えないため包む
        [Serializable]
        private class SavedBindings
        {
            public List<SavedBinding> Items = new();
        }

        /* 定数 */

        public const string LogTag = "Input";
        public const string MapName = "Battle";
        // 1 つの操作がデバイスごとに持つ割り当ての枠の数
        public const int SlotCount = 2;
        public const string KeyboardGroup = "Keyboard&Mouse";
        public const string GamepadGroup = "Gamepad";
        // 未割り当ての枠に出す文字
        public const string EmptyDisplayName = "なし";

        private const string PrefsKey = "PP.InputBindings";

        /* static 変数 */

        // キー設定に並べる操作。分類ごとに、並び順が画面の行の順になる
        public static readonly PPRebindableAction[] RebindableActions =
        {
            new("Submit", "決定", PPRebindCategory.UI),
            new("Cancel", "キャンセル", PPRebindCategory.UI),
            new("DropLeft", "投入位置を左へ", PPRebindCategory.Game),
            new("DropRight", "投入位置を右へ", PPRebindCategory.Game),
            new("Drop", "コインを投入", PPRebindCategory.Game),
        };

        // 操作の場所の名前を短く言い換える表。ゲームパッドは Xbox / PlayStation の両方の呼び名を並べる
        private static readonly Dictionary<string, string> sDisplayNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { "<Gamepad>/buttonSouth", "A/×" },
            { "<Gamepad>/buttonEast", "B/○" },
            { "<Gamepad>/buttonNorth", "Y/△" },
            { "<Gamepad>/buttonWest", "X/□" },
            { "<Gamepad>/leftShoulder", "LB/L1" },
            { "<Gamepad>/rightShoulder", "RB/R1" },
            { "<Gamepad>/leftTrigger", "LT/L2" },
            { "<Gamepad>/rightTrigger", "RT/R2" },
            { "<Gamepad>/leftStickPress", "L3" },
            { "<Gamepad>/rightStickPress", "R3" },
            { "<Gamepad>/select", "Select" },
            { "<Gamepad>/dpad/up", "十字↑" },
            { "<Gamepad>/dpad/down", "十字↓" },
            { "<Gamepad>/dpad/left", "十字←" },
            { "<Gamepad>/dpad/right", "十字→" },
            { "<Gamepad>/leftStick/up", "Lスティック↑" },
            { "<Gamepad>/leftStick/down", "Lスティック↓" },
            { "<Gamepad>/leftStick/left", "Lスティック←" },
            { "<Gamepad>/leftStick/right", "Lスティック→" },
            { "<Keyboard>/upArrow", "↑" },
            { "<Keyboard>/downArrow", "↓" },
            { "<Keyboard>/leftArrow", "←" },
            { "<Keyboard>/rightArrow", "→" },
            { "<Mouse>/rightButton", "右クリック" },
            { "<Mouse>/middleButton", "中クリック" },
        };

        /* 保存・読み込み */

        // 既定の割り当てを当ててから、保存されている変更を読み込む。保存が無ければ既定のまま
        public static void Load(InputActionAsset aAsset)
        {
            ApplyDefaults(aAsset);
            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var saved = JsonUtility.FromJson<SavedBindings>(json);
                foreach (var item in saved.Items)
                {
                    var action = FindAction(aAsset, item.Action);
                    int index = action != null ? GetBindingIndex(action, item.IsGamepad, item.Slot) : -1;
                    if (index >= 0) action.ApplyBindingOverride(index, item.Path ?? string.Empty);
                }
            }
            catch (Exception e)
            {
                // 壊れた保存データで起動できなくならないよう、既定の割り当てに戻して続ける
                CustomConsoleLog.Warning(LogTag, $"キー設定の読み込みに失敗したため、既定の割り当てに戻します。{e.Message}");
                ApplyDefaults(aAsset);
            }
        }

        // 既定と違う枠だけを保存する。既定を変えたとき、プレイヤーが変えていない枠には新しい既定が効くようにするため
        public static void Save(InputActionAsset aAsset)
        {
            var saved = new SavedBindings();
            ForEachSlot(aAsset, (aAction, aIsGamepad, aSlot, aIndex) =>
            {
                string path = aAction.bindings[aIndex].effectivePath ?? string.Empty;
                if (SamePath(path, GetDefaultPath(aAction, aIsGamepad, aSlot))) return;
                saved.Items.Add(new SavedBinding { Action = aAction.name, IsGamepad = aIsGamepad, Slot = aSlot, Path = path });
            });
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(saved));
            PlayerPrefs.Save();
        }

        // 変更できる操作を既定の割り当てに戻して保存する
        public static void ResetToDefault(InputActionAsset aAsset)
        {
            ApplyDefaults(aAsset);
            Save(aAsset);
        }

        // 変更できる操作の変更を消し、PPRuntimeSettings の既定の割り当てを当てる
        private static void ApplyDefaults(InputActionAsset aAsset)
        {
            foreach (var rebindable in RebindableActions) FindAction(aAsset, rebindable.ActionName)?.RemoveAllBindingOverrides();
            ForEachSlot(aAsset, (aAction, aIsGamepad, aSlot, aIndex) =>
            {
                string path = GetDefaultPath(aAction, aIsGamepad, aSlot);
                if (!SamePath(path, aAction.bindings[aIndex].path)) aAction.ApplyBindingOverride(aIndex, path);
            });
        }

        /* 問い合わせ */

        // 変更できる操作の入力アクションを引く。見つからなければ null
        public static InputAction FindAction(InputActionAsset aAsset, string aActionName)
            => aAsset.FindActionMap(MapName)?.FindAction(aActionName);

        // 操作の、指定したデバイスの何枠目にあたる割り当ての番号を返す。無ければ -1
        public static int GetBindingIndex(InputAction aAction, bool aIsGamepad, int aSlot)
        {
            string group = aIsGamepad ? GamepadGroup : KeyboardGroup;
            int count = 0;
            var bindings = aAction.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].isComposite || bindings[i].isPartOfComposite) continue;
                if (!HasGroup(bindings[i], group)) continue;
                if (count == aSlot) return i;
                count++;
            }
            return -1;
        }

        public static string GetCategoryName(PPRebindCategory aCategory) => aCategory == PPRebindCategory.Game ? "ゲーム操作" : "UI操作";

        // 操作の分類を返す。変更できる操作に無ければ null
        public static PPRebindCategory? FindCategory(string aActionName)
        {
            foreach (var rebindable in RebindableActions)
            {
                if (rebindable.ActionName == aActionName) return rebindable.Category;
            }
            return null;
        }

        // 割り当ての場所を、画面に出す短い名前にする。未割り当てなら「なし」
        public static string GetDisplayName(string aPath)
        {
            if (string.IsNullOrEmpty(aPath)) return EmptyDisplayName;
            if (sDisplayNames.TryGetValue(aPath, out var name)) return name;
            return InputControlPath.ToHumanReadableString(aPath, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }

        /* 変更 */

        // 押したキー・ボタンで割り当てを変更する。完了・取り消しのどちらでも、終わったら aOnFinished を呼ぶ
        // Esc で取り消し、一定時間何も押さなければ取り消す
        public static InputActionRebindingExtensions.RebindingOperation StartRebind(InputActionAsset aAsset, InputAction aAction,
            int aBindingIndex, bool aIsGamepad, float aTimeout, Action aOnFinished)
        {
            string oldPath = aAction.bindings[aBindingIndex].effectivePath;
            bool wasEnabled = aAction.enabled;
            aAction.Disable();

            var operation = aAction.PerformInteractiveRebinding(aBindingIndex)
                .WithExpectedControlType("Button")
                .WithControlsExcluding("<Keyboard>/escape")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithControlsExcluding("<Gamepad>/start")
                .WithControlsExcluding("<Mouse>/leftButton")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithTimeout(aTimeout)
                .OnMatchWaitForAnother(0.1f);
            if (aIsGamepad)
            {
                operation.WithControlsHavingToMatchPath("<Gamepad>");
            }
            else
            {
                operation.WithControlsHavingToMatchPath("<Keyboard>");
                operation.WithControlsHavingToMatchPath("<Mouse>");
            }

            void Finish(InputActionRebindingExtensions.RebindingOperation aOperation, bool aIsCompleted)
            {
                if (aIsCompleted)
                {
                    SwapDuplicate(aAsset, aAction, aBindingIndex, aIsGamepad, oldPath);
                    Save(aAsset);
                }
                aOperation.Dispose();
                if (wasEnabled) aAction.Enable();
                aOnFinished?.Invoke();
            }

            operation.OnComplete(op => Finish(op, true));
            operation.OnCancel(op => Finish(op, false));
            return operation.Start();
        }

        // 新しく割り当てたキーを、同じ分類・同じデバイスのほかの枠が使っていたら、その枠へ元のキーを渡して入れ替える
        // 分類が違う操作(UI 操作とゲーム操作)は、同じキーのまま残す
        private static void SwapDuplicate(InputActionAsset aAsset, InputAction aAction, int aBindingIndex, bool aIsGamepad, string aOldPath)
        {
            string newPath = aAction.bindings[aBindingIndex].effectivePath;
            if (string.IsNullOrEmpty(newPath)) return;
            var category = FindCategory(aAction.name);
            foreach (var rebindable in RebindableActions)
            {
                if (rebindable.Category != category) continue;
                var other = FindAction(aAsset, rebindable.ActionName);
                if (other == null) continue;
                for (int slot = 0; slot < SlotCount; slot++)
                {
                    int index = GetBindingIndex(other, aIsGamepad, slot);
                    if (index < 0 || (other == aAction && index == aBindingIndex)) continue;
                    if (!SamePath(other.bindings[index].effectivePath, newPath)) continue;
                    other.ApplyBindingOverride(index, aOldPath ?? string.Empty);
                }
            }
        }

        /* ヘルパー */

        // 枠の既定の場所を返す。PPRuntimeSettings の既定に載っていなければ、入力アクションのアセットに書かれた場所
        private static string GetDefaultPath(InputAction aAction, bool aIsGamepad, int aSlot)
        {
            var config = PPRuntimeSettings.Current.DefaultKeyConfig;
            if (config != null && config.TryGetPath(aAction.name, aIsGamepad, aSlot, out var path)) return path;
            int index = GetBindingIndex(aAction, aIsGamepad, aSlot);
            return index >= 0 ? aAction.bindings[index].path ?? string.Empty : string.Empty;
        }

        // 変更できる操作のすべての枠について処理する(操作, パッドか, 何枠目か, 割り当ての番号)
        private static void ForEachSlot(InputActionAsset aAsset, Action<InputAction, bool, int, int> aVisitor)
        {
            foreach (var rebindable in RebindableActions)
            {
                var action = FindAction(aAsset, rebindable.ActionName);
                if (action == null) continue;
                for (int device = 0; device < 2; device++)
                {
                    for (int slot = 0; slot < SlotCount; slot++)
                    {
                        int index = GetBindingIndex(action, device == 1, slot);
                        if (index >= 0) aVisitor(action, device == 1, slot, index);
                    }
                }
            }
        }

        // 場所の文字列が同じか。未割り当ての null と空文字は同じとみなす
        private static bool SamePath(string aLeft, string aRight)
            => string.Equals(aLeft ?? string.Empty, aRight ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        private static bool HasGroup(InputBinding aBinding, string aGroup)
        {
            if (string.IsNullOrEmpty(aBinding.groups)) return false;
            foreach (var group in aBinding.groups.Split(InputBinding.Separator))
            {
                if (group == aGroup) return true;
            }
            return false;
        }
    }
}
