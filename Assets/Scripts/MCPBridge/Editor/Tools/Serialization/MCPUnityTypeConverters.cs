/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file MCPUnityTypeConverters.cs
 * @author hqrse
 * @date 2026/08/20
 * @brief Unity固有型のJSON相互変換を担うJsonConverter群
 * 1つのコンバーターがWriteJson/ReadJsonを対で持つため、get_fieldで読んだ値を
 * そのままset_fieldへ渡せる(ラウンドトリップ整合)ことが構造的に担保される。
 * 各コンバーターは必ずinternalにする。publicにするとサードパーティのNewtonsoft
 * コンバータースキャナにリフレクションで拾われ、JsonConvert.DefaultSettings経由で
 * プロジェクト全体のシリアライズ挙動を書き換えてしまう
 * =====================================*/

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MCPBridge.Editor.Tools.Serialization
{
    internal sealed class MCPVector2Converter : JsonConverter<Vector2>
    {
        public override void WriteJson(JsonWriter aWriter, Vector2 aValue, JsonSerializer aSerializer)
        {
            aWriter.WriteStartObject();
            aWriter.WritePropertyName("x"); aWriter.WriteValue(aValue.x);
            aWriter.WritePropertyName("y"); aWriter.WriteValue(aValue.y);
            aWriter.WriteEndObject();
        }

        public override Vector2 ReadJson(JsonReader aReader, Type aObjectType, Vector2 aExistingValue, bool aHasExistingValue, JsonSerializer aSerializer)
        {
            var token = JToken.Load(aReader);
            if (token is JArray array && array.Count >= 2)
            {
                return new Vector2((float)array[0], (float)array[1]);
            }
            if (token is not JObject obj)
            {
                throw new JsonSerializationException($"Vector2として解釈できません: {token}");
            }
            return new Vector2(obj["x"]?.Value<float>() ?? 0f, obj["y"]?.Value<float>() ?? 0f);
        }
    }

    internal sealed class MCPVector3Converter : JsonConverter<Vector3>
    {
        public override void WriteJson(JsonWriter aWriter, Vector3 aValue, JsonSerializer aSerializer)
        {
            aWriter.WriteStartObject();
            aWriter.WritePropertyName("x"); aWriter.WriteValue(aValue.x);
            aWriter.WritePropertyName("y"); aWriter.WriteValue(aValue.y);
            aWriter.WritePropertyName("z"); aWriter.WriteValue(aValue.z);
            aWriter.WriteEndObject();
        }

        public override Vector3 ReadJson(JsonReader aReader, Type aObjectType, Vector3 aExistingValue, bool aHasExistingValue, JsonSerializer aSerializer)
        {
            var token = JToken.Load(aReader);
            if (token is JArray array && array.Count >= 3)
            {
                return new Vector3((float)array[0], (float)array[1], (float)array[2]);
            }
            if (token is not JObject obj)
            {
                throw new JsonSerializationException($"Vector3として解釈できません: {token}");
            }
            return new Vector3(
                obj["x"]?.Value<float>() ?? 0f,
                obj["y"]?.Value<float>() ?? 0f,
                obj["z"]?.Value<float>() ?? 0f);
        }
    }

    internal sealed class MCPVector4Converter : JsonConverter<Vector4>
    {
        public override void WriteJson(JsonWriter aWriter, Vector4 aValue, JsonSerializer aSerializer)
        {
            aWriter.WriteStartObject();
            aWriter.WritePropertyName("x"); aWriter.WriteValue(aValue.x);
            aWriter.WritePropertyName("y"); aWriter.WriteValue(aValue.y);
            aWriter.WritePropertyName("z"); aWriter.WriteValue(aValue.z);
            aWriter.WritePropertyName("w"); aWriter.WriteValue(aValue.w);
            aWriter.WriteEndObject();
        }

        public override Vector4 ReadJson(JsonReader aReader, Type aObjectType, Vector4 aExistingValue, bool aHasExistingValue, JsonSerializer aSerializer)
        {
            var token = JToken.Load(aReader);
            if (token is JArray array && array.Count >= 4)
            {
                return new Vector4((float)array[0], (float)array[1], (float)array[2], (float)array[3]);
            }
            if (token is not JObject obj)
            {
                throw new JsonSerializationException($"Vector4として解釈できません: {token}");
            }
            return new Vector4(
                obj["x"]?.Value<float>() ?? 0f,
                obj["y"]?.Value<float>() ?? 0f,
                obj["z"]?.Value<float>() ?? 0f,
                obj["w"]?.Value<float>() ?? 0f);
        }
    }

    internal sealed class MCPVector2IntConverter : JsonConverter<Vector2Int>
    {
        public override void WriteJson(JsonWriter aWriter, Vector2Int aValue, JsonSerializer aSerializer)
        {
            aWriter.WriteStartObject();
            aWriter.WritePropertyName("x"); aWriter.WriteValue(aValue.x);
            aWriter.WritePropertyName("y"); aWriter.WriteValue(aValue.y);
            aWriter.WriteEndObject();
        }

        public override Vector2Int ReadJson(JsonReader aReader, Type aObjectType, Vector2Int aExistingValue, bool aHasExistingValue, JsonSerializer aSerializer)
        {
            var token = JToken.Load(aReader);
            if (token is JArray array && array.Count >= 2)
            {
                return new Vector2Int((int)array[0], (int)array[1]);
            }
            if (token is not JObject obj)
            {
                throw new JsonSerializationException($"Vector2Intとして解釈できません: {token}");
            }
            return new Vector2Int(obj["x"]?.Value<int>() ?? 0, obj["y"]?.Value<int>() ?? 0);
        }
    }

    internal sealed class MCPVector3IntConverter : JsonConverter<Vector3Int>
    {
        public override void WriteJson(JsonWriter aWriter, Vector3Int aValue, JsonSerializer aSerializer)
        {
            aWriter.WriteStartObject();
            aWriter.WritePropertyName("x"); aWriter.WriteValue(aValue.x);
            aWriter.WritePropertyName("y"); aWriter.WriteValue(aValue.y);
            aWriter.WritePropertyName("z"); aWriter.WriteValue(aValue.z);
            aWriter.WriteEndObject();
        }

        public override Vector3Int ReadJson(JsonReader aReader, Type aObjectType, Vector3Int aExistingValue, bool aHasExistingValue, JsonSerializer aSerializer)
        {
            var token = JToken.Load(aReader);
            if (token is JArray array && array.Count >= 3)
            {
                return new Vector3Int((int)array[0], (int)array[1], (int)array[2]);
            }
            if (token is not JObject obj)
            {
                throw new JsonSerializationException($"Vector3Intとして解釈できません: {token}");
            }
            return new Vector3Int(
                obj["x"]?.Value<int>() ?? 0,
                obj["y"]?.Value<int>() ?? 0,
                obj["z"]?.Value<int>() ?? 0);
        }
    }

    internal sealed class MCPQuaternionConverter : JsonConverter<Quaternion>
    {
        public override void WriteJson(JsonWriter aWriter, Quaternion aValue, JsonSerializer aSerializer)
        {
            aWriter.WriteStartObject();
            aWriter.WritePropertyName("x"); aWriter.WriteValue(aValue.x);
            aWriter.WritePropertyName("y"); aWriter.WriteValue(aValue.y);
            aWriter.WritePropertyName("z"); aWriter.WriteValue(aValue.z);
            aWriter.WritePropertyName("w"); aWriter.WriteValue(aValue.w);
            aWriter.WriteEndObject();
        }

        public override Quaternion ReadJson(JsonReader aReader, Type aObjectType, Quaternion aExistingValue, bool aHasExistingValue, JsonSerializer aSerializer)
        {
            var token = JToken.Load(aReader);
            if (token is JArray array && array.Count >= 4)
            {
                return new Quaternion((float)array[0], (float)array[1], (float)array[2], (float)array[3]);
            }
            if (token is not JObject obj)
            {
                throw new JsonSerializationException($"Quaternionとして解釈できません: {token}");
            }
            return new Quaternion(
                obj["x"]?.Value<float>() ?? 0f,
                obj["y"]?.Value<float>() ?? 0f,
                obj["z"]?.Value<float>() ?? 0f,
                obj["w"]?.Value<float>() ?? 0f);
        }
    }

    internal sealed class MCPColorConverter : JsonConverter<Color>
    {
        public override void WriteJson(JsonWriter aWriter, Color aValue, JsonSerializer aSerializer)
        {
            aWriter.WriteStartObject();
            aWriter.WritePropertyName("r"); aWriter.WriteValue(aValue.r);
            aWriter.WritePropertyName("g"); aWriter.WriteValue(aValue.g);
            aWriter.WritePropertyName("b"); aWriter.WriteValue(aValue.b);
            aWriter.WritePropertyName("a"); aWriter.WriteValue(aValue.a);
            aWriter.WriteEndObject();
        }

        public override Color ReadJson(JsonReader aReader, Type aObjectType, Color aExistingValue, bool aHasExistingValue, JsonSerializer aSerializer)
        {
            var token = JToken.Load(aReader);
            if (token is JArray array && array.Count >= 3)
            {
                return new Color(
                    (float)array[0], (float)array[1], (float)array[2],
                    array.Count >= 4 ? (float)array[3] : 1f);
            }
            if (token is not JObject obj)
            {
                throw new JsonSerializationException($"Colorとして解釈できません: {token}");
            }
            return new Color(
                obj["r"]?.Value<float>() ?? 0f,
                obj["g"]?.Value<float>() ?? 0f,
                obj["b"]?.Value<float>() ?? 0f,
                obj["a"]?.Value<float>() ?? 1f);
        }
    }

    internal sealed class MCPRectConverter : JsonConverter<Rect>
    {
        public override void WriteJson(JsonWriter aWriter, Rect aValue, JsonSerializer aSerializer)
        {
            aWriter.WriteStartObject();
            aWriter.WritePropertyName("x"); aWriter.WriteValue(aValue.x);
            aWriter.WritePropertyName("y"); aWriter.WriteValue(aValue.y);
            aWriter.WritePropertyName("width"); aWriter.WriteValue(aValue.width);
            aWriter.WritePropertyName("height"); aWriter.WriteValue(aValue.height);
            aWriter.WriteEndObject();
        }

        public override Rect ReadJson(JsonReader aReader, Type aObjectType, Rect aExistingValue, bool aHasExistingValue, JsonSerializer aSerializer)
        {
            var token = JToken.Load(aReader);
            if (token is not JObject obj)
            {
                throw new JsonSerializationException($"Rectとして解釈できません: {token}");
            }
            return new Rect(
                obj["x"]?.Value<float>() ?? 0f,
                obj["y"]?.Value<float>() ?? 0f,
                obj["width"]?.Value<float>() ?? 0f,
                obj["height"]?.Value<float>() ?? 0f);
        }
    }

    internal sealed class MCPBoundsConverter : JsonConverter<Bounds>
    {
        public override void WriteJson(JsonWriter aWriter, Bounds aValue, JsonSerializer aSerializer)
        {
            aWriter.WriteStartObject();
            // 入れ子のVector3もコンバーター経由で書けるようserializerへ委譲する
            aWriter.WritePropertyName("center"); aSerializer.Serialize(aWriter, aValue.center);
            aWriter.WritePropertyName("size"); aSerializer.Serialize(aWriter, aValue.size);
            aWriter.WriteEndObject();
        }

        public override Bounds ReadJson(JsonReader aReader, Type aObjectType, Bounds aExistingValue, bool aHasExistingValue, JsonSerializer aSerializer)
        {
            var token = JToken.Load(aReader);
            if (token is not JObject obj)
            {
                throw new JsonSerializationException($"Boundsとして解釈できません: {token}");
            }
            var center = obj["center"]?.ToObject<Vector3>(aSerializer) ?? Vector3.zero;
            var size = obj["size"]?.ToObject<Vector3>(aSerializer) ?? Vector3.zero;
            return new Bounds(center, size);
        }
    }

    // lossyScale/rotation/inverse等の計算プロパティは内部でValidTRS()を呼び、
    // 非TRS行列(Cinemachine系のコンポーネント等で現れる)に対してUnityごとクラッシュする。
    // 生の要素m00〜m33のみを読み書きする
    internal sealed class MCPMatrix4x4Converter : JsonConverter<Matrix4x4>
    {
        public override void WriteJson(JsonWriter aWriter, Matrix4x4 aValue, JsonSerializer aSerializer)
        {
            aWriter.WriteStartObject();
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    aWriter.WritePropertyName($"m{row}{column}");
                    aWriter.WriteValue(aValue[row, column]);
                }
            }
            aWriter.WriteEndObject();
        }

        public override Matrix4x4 ReadJson(JsonReader aReader, Type aObjectType, Matrix4x4 aExistingValue, bool aHasExistingValue, JsonSerializer aSerializer)
        {
            var token = JToken.Load(aReader);
            if (token is not JObject obj)
            {
                throw new JsonSerializationException($"Matrix4x4として解釈できません: {token}");
            }

            var matrix = new Matrix4x4();
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    matrix[row, column] = obj[$"m{row}{column}"]?.Value<float>() ?? 0f;
                }
            }
            return matrix;
        }
    }

    // アセットはアセットパス文字列、シーンオブジェクトは{name, instanceID}として書き出し、
    // 読み戻し時はアセットパス/GUID/instanceIDのいずれからでも解決する
    internal sealed class MCPUnityObjectConverter : JsonConverter<UnityEngine.Object>
    {
        public override void WriteJson(JsonWriter aWriter, UnityEngine.Object aValue, JsonSerializer aSerializer)
        {
            if (aValue == null)
            {
                aWriter.WriteNull();
                return;
            }

            if (AssetDatabase.Contains(aValue))
            {
                var path = AssetDatabase.GetAssetPath(aValue);
                if (!string.IsNullOrEmpty(path))
                {
                    aWriter.WriteValue(path);
                    return;
                }
            }

            aWriter.WriteStartObject();
            aWriter.WritePropertyName("name"); aWriter.WriteValue(aValue.name);
            aWriter.WritePropertyName("type"); aWriter.WriteValue(aValue.GetType().Name);
            aWriter.WritePropertyName("instanceID"); aWriter.WriteValue(aValue.GetInstanceID());
            aWriter.WriteEndObject();
        }

        public override UnityEngine.Object ReadJson(JsonReader aReader, Type aObjectType, UnityEngine.Object aExistingValue, bool aHasExistingValue, JsonSerializer aSerializer)
        {
            var token = JToken.Load(aReader);
            return MCPUnityObjectResolver.Resolve(token, aObjectType);
        }
    }
}
