/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitCreatorTools.cs
 * @author hqrse
 * @date 2026/09/09
 * @brief ユニット下書きの共通サービスへ接続するMCPアダプター
 * =====================================*/

using System;
using System.Linq;
using MCPBridge.Editor.Server;
using MCPBridge.Editor.Tools;
using Newtonsoft.Json.Linq;
using PPCore;
using UnityEditor;
using UnityEngine;

namespace MCPBridgeBindings.Editor
{
    public sealed class PPUnitCreatorDraftTool : IMCPTool
    {
        public string Name => "pp_unit_creator_draft";
        public string Description => "ユニット下書きのcreate/get/update/evaluate/validate/open。updateはexpectedRevision必須。patchで共通入力、stats、skillsを編集。生成はpp_unit_creator_generateを使用。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["operation"] = new JObject { ["type"] = "string", ["enum"] = new JArray("create", "get", "update", "evaluate", "validate", "open") },
                ["draftId"] = new JObject { ["type"] = "string" },
                ["expectedRevision"] = new JObject { ["type"] = "integer" },
                ["levels"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "integer", ["minimum"] = 1 } },
                ["patch"] = PPUnitCreatorAdapter.PatchSchema,
            },
            ["required"] = new JArray("operation"),
            ["additionalProperties"] = false,
        };

        public JToken Invoke(JObject aArguments) => MCPMainThreadDispatcher.RunOnMainThread(() =>
        {
            var operation = aArguments.Value<string>("operation");
            if (aArguments["patch"] != null && !(aArguments["patch"] is JObject)) throw new ArgumentException("patchはオブジェクトで指定してください。");
            if (operation == "create")
            {
                var created = PPUnitCreationDraft.Create();
                try
                {
                    if (aArguments["patch"] is JObject patch) PPUnitCreatorAdapter.Apply(created, patch);
                    var description = PPUnitCreatorAdapter.Describe(created);
                    PPUnitDraftStore.Save(created);
                    return description;
                }
                catch { PPUnitCreatorAdapter.DestroyDraft(created); throw; }
            }
            var draft = PPUnitDraftStore.Load(aArguments.Value<string>("draftId") ?? "");
            switch (operation)
            {
                case "get": return PPUnitCreatorAdapter.Describe(draft);
                case "update":
                    if (aArguments["expectedRevision"] == null) throw new ArgumentException("expectedRevisionが必要です。");
                    draft.RequireRevision(aArguments.Value<int>("expectedRevision"));
                    var copy = draft.CreateWorkingCopy();
                    try
                    {
                        PPUnitCreatorAdapter.Apply(copy, aArguments["patch"] as JObject ?? throw new ArgumentException("patchが必要です。"));
                        var description = PPUnitCreatorAdapter.Describe(copy);
                        copy.Commit();
                        description["revision"] = copy.Revision;
                        return description;
                    }
                    catch { PPUnitCreatorAdapter.DestroyDraft(copy); throw; }
                case "evaluate":
                    var levels = aArguments["levels"]?.ToObject<int[]>() ?? new[] { 1, draft.PreviewLevel, draft.MaxLevel };
                    if (levels.Length > 1000 || levels.Any(aLevel => aLevel < 1 || aLevel > draft.MaxLevel)) throw new ArgumentException("levelsは下書きのレベル範囲内、1000件以下で指定してください。");
                    return new JObject
                    {
                        ["revision"] = draft.Revision,
                        ["values"] = new JArray(levels.Select(aLevel => new JObject { ["level"] = aLevel, ["stats"] = JObject.FromObject(draft.Unit.EvaluateStats(aLevel)) })),
                        ["validation"] = PPUnitCreatorAdapter.Validation(draft),
                    };
                case "validate": return PPUnitCreatorAdapter.Validation(draft);
                case "open": PPUnitCreatorWindow.OpenDraft(draft.DraftId); return PPUnitCreatorAdapter.Describe(draft);
                default: throw new ArgumentException("未知の操作です: " + operation);
            }
        });
    }

    public sealed class PPUnitCreatorGenerateTool : IMCPTool
    {
        public string Name => "pp_unit_creator_generate";
        public string Description => "検証済み下書きからユニットと関連アセットを生成。永続化操作。expectedRevisionとrequestId必須。同じ要求の再送は記録済み結果を返す。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["draftId"] = new JObject { ["type"] = "string" },
                ["expectedRevision"] = new JObject { ["type"] = "integer" },
                ["requestId"] = new JObject { ["type"] = "string", ["minLength"] = 1 },
            },
            ["required"] = new JArray("draftId", "expectedRevision", "requestId"),
            ["additionalProperties"] = false,
        };

        public JToken Invoke(JObject aArguments) => MCPMainThreadDispatcher.RunOnMainThread(() =>
        {
            if (aArguments["expectedRevision"] == null) throw new ArgumentException("expectedRevisionが必要です。");
            var draft = PPUnitDraftStore.Load(aArguments.Value<string>("draftId") ?? "");
            var paths = PPUnitCreationService.Create(draft, aArguments.Value<int>("expectedRevision"), aArguments.Value<string>("requestId"));
            return new JObject
            {
                ["draftId"] = draft.DraftId, ["unitId"] = draft.CreatedUnitId, ["revision"] = draft.CreatedRevision,
                ["assets"] = new JArray(paths.Select((aPath, aIndex) => new JObject { ["path"] = aPath, ["guid"] = draft.CreatedGuids[aIndex] })),
                ["unitCatalogRegistered"] = draft.UnitCatalog != null,
                ["visualCatalogRegistered"] = draft.VisualCatalog != null,
                ["skillCatalogRegistered"] = draft.SkillCatalog != null,
            };
        });
    }

    public static class PPUnitCreatorAdapter
    {
        // 詳細データのスキーマは設定対象と共に返し、推測によるパス指定を避ける
        public static JObject PatchSchema => new()
        {
            ["type"] = "object", ["additionalProperties"] = false,
            ["properties"] = new JObject
            {
                ["unitId"] = new JObject { ["type"] = "string" }, ["displayName"] = new JObject { ["type"] = "string" },
                ["folder"] = new JObject { ["type"] = "string" }, ["maxLevel"] = new JObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 100000 },
                ["previewLevel"] = new JObject { ["type"] = "integer" }, ["newAI"] = new JObject { ["type"] = "boolean" },
                ["typeAttribute"] = new JObject { ["type"] = "string", ["enum"] = new JArray(Enum.GetNames(typeof(PPTypeAttribute))) },
                ["icon"] = ReferenceSchema(), ["aiProfile"] = ReferenceSchema(),
                ["unitCatalog"] = ReferenceSchema(), ["visualCatalog"] = ReferenceSchema(), ["skillCatalog"] = ReferenceSchema(),
                ["scales"] = new JObject { ["type"] = "array", ["minItems"] = 4, ["maxItems"] = 4, ["items"] = new JObject { ["type"] = "number", ["exclusiveMinimum"] = 0 } },
                ["extra"] = new JObject { ["type"] = "object", ["description"] = "AttackCost, ActionCount, SkillGaugeMax, CoinGaugeMax" },
                ["stats"] = new JObject { ["type"] = "array", ["description"] = "各要素: index(0=HP,1=攻撃,2=防御,3=速度), initial, final, template(線形/早熟/晩成/S字), curve({keys:[{time,value,inTangent,outTangent,inWeight,outWeight,weightedMode}],preWrapMode,postWrapMode})。templateとcurveは同時指定不可。" },
                ["skills"] = new JObject { ["type"] = "array", ["description"] = "一覧を置換。各要素は{reference:{guid,localFileId}}または{new:{mSkillId:...,mDisplayName:...,mSkillEffects:[{type:完全型名,properties:{...}}]}}。get結果のpropertiesはそのままnewへ渡せる。" },
            },
        };

        private static JObject ReferenceSchema() => new()
        {
            ["type"] = new JArray("object", "null"),
            ["properties"] = new JObject { ["guid"] = new JObject { ["type"] = "string" }, ["localFileId"] = new JObject { ["type"] = "integer" } },
            ["required"] = new JArray("guid", "localFileId"),
        };

        public static JObject Validation(PPUnitCreationDraft aDraft)
        {
            var result = PPUnitCreationValidator.Validate(aDraft);
            return new JObject { ["revision"] = aDraft.Revision, ["errors"] = new JArray(result.Errors), ["warnings"] = new JArray(result.Warnings), ["paths"] = new JArray(result.Paths) };
        }

        // GUIDとlocalFileIdの組でサブアセットを厳密に解決する
        public static T Resolve<T>(JToken aToken) where T : UnityEngine.Object
        {
            if (aToken == null || aToken.Type == JTokenType.Null) return null;
            var guid = aToken.Value<string>("guid");
            if (string.IsNullOrEmpty(guid) || aToken["localFileId"] == null) throw new ArgumentException("参照にはguidとlocalFileIdが必要です。");
            var id = aToken.Value<long>("localFileId");
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var result = AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().FirstOrDefault(aObject =>
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(aObject, out string objectGuid, out long localId) && objectGuid == guid && localId == id);
            if (result == null) throw new ArgumentException("指定した型の参照を解決できません: " + guid + "/" + id);
            return result;
        }

        public static JToken Reference(UnityEngine.Object aObject)
        {
            if (aObject == null) return JValue.CreateNull();
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(aObject, out string guid, out long id)) return JValue.CreateNull();
            return new JObject { ["guid"] = guid, ["localFileId"] = id };
        }

        public static JObject Describe(PPUnitCreationDraft aDraft) => new()
        {
            ["draftId"] = aDraft.DraftId, ["revision"] = aDraft.Revision,
            ["unitId"] = aDraft.Unit.UnitId, ["displayName"] = aDraft.Unit.DisplayName,
            ["folder"] = aDraft.Folder, ["maxLevel"] = aDraft.MaxLevel, ["previewLevel"] = aDraft.PreviewLevel,
            ["typeAttribute"] = aDraft.Unit.TypeAttribute.ToString(), ["icon"] = Reference(aDraft.Icon),
            ["newAI"] = aDraft.NewAI, ["aiProfile"] = Reference(aDraft.Unit.AIProfile),
            ["unitCatalog"] = Reference(aDraft.UnitCatalog), ["visualCatalog"] = Reference(aDraft.VisualCatalog), ["skillCatalog"] = Reference(aDraft.SkillCatalog),
            ["scales"] = new JArray(aDraft.Scales), ["extra"] = JObject.FromObject(aDraft.Unit.ExpandStatBlock),
            ["stats"] = new JArray(Enumerable.Range(0, 4).Select(aIndex => new JObject
            {
                ["index"] = aIndex, ["initial"] = aDraft.GetInitial(aIndex), ["final"] = aDraft.GetFinal(aIndex), ["curve"] = CurveData(aDraft.GetCurve(aIndex)),
            })),
            ["skills"] = new JArray(aDraft.Unit.Skills.Select(aSkill => aSkill is PPSkillDefinition skill && aDraft.NewSkills.Contains(skill)
                ? new JObject { ["new"] = ReadProperties(new SerializedObject(skill)) }
                : new JObject { ["reference"] = Reference(aSkill) })),
            ["createdPaths"] = new JArray(aDraft.CreatedPaths),
        };

        public static void Apply(PPUnitCreationDraft aDraft, JObject aPatch)
        {
            var allowed = (JObject)PatchSchema["properties"];
            foreach (var field in aPatch.Properties())
            {
                if (allowed[field.Name] == null) throw new ArgumentException("未知の変更項目: " + field.Name);
                var expected = allowed[field.Name]["type"];
                var actual = field.Value.Type switch
                {
                    JTokenType.Integer => "integer", JTokenType.Float => "number", JTokenType.String => "string",
                    JTokenType.Array => "array", JTokenType.Object => "object", JTokenType.Boolean => "boolean", JTokenType.Null => "null", _ => "invalid",
                };
                if (expected is JArray accepted ? !accepted.Values<string>().Contains(actual) : expected.Value<string>() != actual)
                    throw new ArgumentException("項目の型が不正です: " + field.Name);
            }
            if (aPatch["maxLevel"] != null) aDraft.SetMaxLevel(aPatch.Value<int>("maxLevel"));
            if (aPatch["previewLevel"] != null) aDraft.PreviewLevel = aPatch.Value<int>("previewLevel");
            if (aPatch["folder"] != null) aDraft.Folder = aPatch.Value<string>("folder") ?? "";
            if (aPatch["newAI"] != null) aDraft.NewAI = aPatch.Value<bool>("newAI");
            if (aPatch["icon"] != null) aDraft.Icon = Resolve<Sprite>(aPatch["icon"]);
            if (aPatch["unitCatalog"] != null) aDraft.UnitCatalog = Resolve<PPUnitCatalog>(aPatch["unitCatalog"]);
            if (aPatch["visualCatalog"] != null) aDraft.VisualCatalog = Resolve<PPUnitVisualCatalog>(aPatch["visualCatalog"]);
            if (aPatch["skillCatalog"] != null) aDraft.SkillCatalog = Resolve<PPSkillCatalog>(aPatch["skillCatalog"]);
            var so = new SerializedObject(aDraft.Unit);
            if (aPatch["unitId"] != null) so.FindProperty("mUnitId").stringValue = aPatch.Value<string>("unitId");
            if (aPatch["displayName"] != null) so.FindProperty("mDisplayName").stringValue = aPatch.Value<string>("displayName");
            if (aPatch["typeAttribute"] != null)
            {
                if (!Enum.TryParse<PPTypeAttribute>(aPatch.Value<string>("typeAttribute"), out var type) || !Enum.IsDefined(typeof(PPTypeAttribute), type)) throw new ArgumentException("不正な属性です。");
                so.FindProperty("mTypeAttribute").intValue = (int)type;
            }
            if (aPatch["aiProfile"] != null) so.FindProperty("mAIProfile").objectReferenceValue = Resolve<PPUnitAIProfileDefinition>(aPatch["aiProfile"]);
            if (aPatch["extra"] is JObject extra) ApplyValue(so.FindProperty("mExpandStatBlock"), extra);
            so.ApplyModifiedPropertiesWithoutUndo();
            if (aPatch["scales"] is JArray scales)
            {
                if (scales.Count != 4) throw new ArgumentException("scalesは4要素です。");
                for (var i = 0; i < 4; i++) aDraft.Scales[i] = scales[i].Value<float>();
            }
            if (aPatch["stats"] is JArray stats)
            {
                foreach (var entry in stats)
                {
                    if (entry["index"] == null) throw new ArgumentException("statsにはindexが必要です。");
                    var index = entry.Value<int>("index");
                    if (index < 0 || index > 3) throw new ArgumentException("stats.indexは0〜3です。");
                    var initial = entry["initial"]?.Value<float>() ?? aDraft.GetInitial(index);
                    var final = entry["final"]?.Value<float>() ?? (aDraft.MaxLevel == 1 ? initial : aDraft.GetFinal(index));
                    if (!PPUnitCreationValidator.IsFinite(initial) || !PPUnitCreationValidator.IsFinite(final) || initial < 0 || final < initial || (initial == 0 && final != 0) || (aDraft.MaxLevel == 1 && initial != final)) throw new ArgumentException("初期値と終端値の組が不正です。");
                    aDraft.SetStat(index, initial, final);
                    if (entry["template"] != null && entry["curve"] != null) throw new ArgumentException("templateとcurveは同時指定できません。");
                    if (entry["template"] != null)
                    {
                        var kind = Array.IndexOf(PPUnitGrowthTemplate.Names, entry.Value<string>("template"));
                        if (kind < 0) throw new ArgumentException("不明な成長テンプレートです。");
                        aDraft.SetCurve(index, PPUnitGrowthTemplate.Create(kind, aDraft.MaxLevel, initial > 0 ? final / initial : 1));
                    }
                    if (entry["curve"] != null) aDraft.SetCurve(index, ReadCurve(entry["curve"]));
                }
            }
            if (aPatch["skills"] is JArray skills)
            {
                if (skills.Count > 256) throw new ArgumentException("スキルは256件以下です。");
                aDraft.Unit.Skills.Clear();
                foreach (var skill in aDraft.NewSkills) UnityEngine.Object.DestroyImmediate(skill);
                aDraft.NewSkills.Clear();
                foreach (var entry in skills)
                {
                    if (entry["reference"] != null && entry["new"] != null) throw new ArgumentException("スキルはreferenceまたはnewを指定してください。");
                    if (entry["new"] is JObject properties)
                    {
                        var skill = aDraft.AddNewSkill();
                        var skillSo = new SerializedObject(skill);
                        foreach (var property in properties.Properties())
                        {
                            if (property.Name == "m_Script") throw new ArgumentException("スクリプトの変更はできません。");
                            ApplyValue(skillSo.FindProperty(property.Name) ?? throw new ArgumentException("不明なスキル項目: " + property.Name), property.Value);
                        }
                        skillSo.ApplyModifiedPropertiesWithoutUndo();
                    }
                    else aDraft.Unit.Skills.Add(Resolve<PPSkillDefinition>(entry["reference"] ?? throw new ArgumentException("スキル参照が必要です。")));
                }
            }
        }

        // スキル効果も既存SerializedPropertyへ設定し、ランタイム生成は行わない
        private static void ApplyValue(SerializedProperty aProperty, JToken aValue)
        {
            if (aProperty.isArray && aProperty.propertyType != SerializedPropertyType.String)
            {
                var array = aValue as JArray ?? throw new ArgumentException("配列が必要です: " + aProperty.propertyPath);
                if (array.Count > 1024) throw new ArgumentException("配列が大きすぎます。");
                aProperty.arraySize = array.Count;
                for (var i = 0; i < array.Count; i++) ApplyValue(aProperty.GetArrayElementAtIndex(i), array[i]);
            }
            else if (aProperty.propertyType == SerializedPropertyType.ManagedReference)
            {
                if (aValue.Type == JTokenType.Null) { aProperty.managedReferenceValue = null; return; }
                var typeName = aValue.Value<string>("type");
                var fieldType = aProperty.managedReferenceFieldTypename.Split(' ');
                var baseType = Type.GetType(fieldType[1] + ", " + fieldType[0]);
                var type = baseType == null ? null : TypeCache.GetTypesDerivedFrom(baseType).FirstOrDefault(aType => aType.FullName == typeName && !aType.IsAbstract && aType.IsSerializable && aType.Namespace == "PPCore");
                if (type == null) throw new ArgumentException("不明なスキル効果型: " + typeName);
                aProperty.managedReferenceValue = Activator.CreateInstance(type);
                foreach (var child in ((JObject)aValue["properties"]).Properties())
                    ApplyValue(aProperty.FindPropertyRelative(child.Name) ?? throw new ArgumentException("不明な効果項目: " + child.Name), child.Value);
            }
            else if (aProperty.propertyType == SerializedPropertyType.Generic)
            {
                foreach (var child in ((JObject)aValue).Properties())
                    ApplyValue(aProperty.FindPropertyRelative(child.Name) ?? throw new ArgumentException("不明な項目: " + child.Name), child.Value);
            }
            else if (aProperty.propertyType == SerializedPropertyType.ObjectReference)
            {
                var value = Resolve<UnityEngine.Object>(aValue);
                if (value != null && aProperty.type.StartsWith("PPtr<", StringComparison.Ordinal))
                {
                    var expectedName = aProperty.type.Substring(5).TrimEnd('>').TrimStart('$');
                    var actualType = value.GetType();
                    while (actualType != null && actualType.Name != expectedName) actualType = actualType.BaseType;
                    if (actualType == null) throw new ArgumentException("参照の型が一致しません: " + aProperty.propertyPath);
                }
                // SerializedPropertyに期待型の検査を委ね、型不一致で無視された場合もエラーにする
                aProperty.objectReferenceValue = value;
                if (aProperty.objectReferenceValue != value) throw new ArgumentException("参照の型が一致しません。");
            }
            else MCPArgumentConverter.ApplyToSerializedProperty(aProperty, aValue);
        }

        private static JObject ReadProperties(SerializedObject aObject)
        {
            var result = new JObject();
            var property = aObject.GetIterator();
            if (property.NextVisible(true)) do
            {
                if (property.name != "m_Script") result[property.name] = ReadValue(property);
            } while (property.NextVisible(false));
            return result;
        }

        private static JToken ReadValue(SerializedProperty aProperty)
        {
            if (aProperty.isArray && aProperty.propertyType != SerializedPropertyType.String)
                return new JArray(Enumerable.Range(0, aProperty.arraySize).Select(aIndex => ReadValue(aProperty.GetArrayElementAtIndex(aIndex))));
            switch (aProperty.propertyType)
            {
                case SerializedPropertyType.String: return aProperty.stringValue;
                case SerializedPropertyType.Float: return aProperty.floatValue;
                case SerializedPropertyType.Integer: return aProperty.intValue;
                case SerializedPropertyType.Boolean: return aProperty.boolValue;
                case SerializedPropertyType.Enum: return aProperty.intValue;
                case SerializedPropertyType.ObjectReference: return Reference(aProperty.objectReferenceValue);
                case SerializedPropertyType.ManagedReference:
                case SerializedPropertyType.Generic:
                    if (aProperty.propertyType == SerializedPropertyType.ManagedReference && aProperty.managedReferenceValue == null) return JValue.CreateNull();
                    var properties = new JObject();
                    var child = aProperty.Copy();
                    var end = child.GetEndProperty();
                    if (child.NextVisible(true)) do
                    {
                        if (SerializedProperty.EqualContents(child, end)) break;
                        properties[child.name] = ReadValue(child);
                    } while (child.NextVisible(false));
                    return aProperty.propertyType == SerializedPropertyType.ManagedReference
                        ? new JObject { ["type"] = aProperty.managedReferenceValue.GetType().FullName, ["properties"] = properties } : properties;
                default: throw new ArgumentException("取得非対応のスキル項目: " + aProperty.propertyPath + " / " + aProperty.propertyType);
            }
        }

        private static JObject CurveData(AnimationCurve aCurve) => new()
        {
            ["preWrapMode"] = (int)aCurve.preWrapMode, ["postWrapMode"] = (int)aCurve.postWrapMode,
            ["keys"] = new JArray(aCurve.keys.Select(aKey => new JObject
            {
                ["time"] = aKey.time, ["value"] = aKey.value, ["inTangent"] = aKey.inTangent, ["outTangent"] = aKey.outTangent,
                ["inWeight"] = aKey.inWeight, ["outWeight"] = aKey.outWeight, ["weightedMode"] = (int)aKey.weightedMode,
            })),
        };

        private static AnimationCurve ReadCurve(JToken aData)
        {
            var keys = aData["keys"] as JArray ?? throw new ArgumentException("curve.keysが必要です。");
            if (keys.Count > 1024) throw new ArgumentException("曲線キーは1024件以下です。");
            return new AnimationCurve(keys.Select(aKey => new Keyframe(aKey.Value<float>("time"), aKey.Value<float>("value"), aKey["inTangent"]?.Value<float>() ?? 0, aKey["outTangent"]?.Value<float>() ?? 0)
            {
                inWeight = aKey["inWeight"]?.Value<float>() ?? 0, outWeight = aKey["outWeight"]?.Value<float>() ?? 0,
                weightedMode = (WeightedMode)(aKey["weightedMode"]?.Value<int>() ?? 0),
            }).ToArray())
            {
                preWrapMode = (WrapMode)(aData["preWrapMode"]?.Value<int>() ?? 0), postWrapMode = (WrapMode)(aData["postWrapMode"]?.Value<int>() ?? 0),
            };
        }

        public static void DestroyDraft(PPUnitCreationDraft aDraft)
        {
            foreach (var skill in aDraft.NewSkills) if (skill != null) UnityEngine.Object.DestroyImmediate(skill);
            if (aDraft.Unit != null) UnityEngine.Object.DestroyImmediate(aDraft.Unit);
            UnityEngine.Object.DestroyImmediate(aDraft);
        }
    }
}
