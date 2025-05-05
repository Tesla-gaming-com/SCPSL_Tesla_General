using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Tesla_General.MyNewtonsoft
{
    /// <summary>
    /// RU: Минимальная реализация JSON-логики (аналог Newtonsoft) без внешних зависимостей. 
    ///     Здесь можно доработать парсер (добавить поддержку сложных типов), 
    ///     оптимизировать сериализацию, и т.д.
    /// EN: Minimal JSON logic implementation (like a lightweight Newtonsoft) without external dependencies.
    ///     You can extend the parser (add complex type support), optimize serialization, etc.
    /// </summary>
    public abstract class JToken
    {
        public JToken Parent { get; internal set; }
        public abstract override string ToString();
        public T ToObject<T>()
        {
            return (T)ToObject(typeof(T));
        }
        internal abstract object ToObject(Type targetType);
    }

    public class JValue : JToken
    {
        public object Value { get; set; }

        public JValue(object value)
        {
            Value = value;
        }

        public override string ToString()
        {
            return MyJsonConvert.SerializePrimitive(Value);
        }

        internal override object ToObject(Type targetType)
        {
            if (Value == null)
            {
                if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                {
                    return Activator.CreateInstance(targetType);
                }
                return null;
            }

            if (targetType == typeof(string))
                return Value.ToString();

            if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
            {
                DateTime dt;
                if (DateTime.TryParse(Value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt))
                    return dt;
                return DateTime.MinValue;
            }
            if (targetType == typeof(bool) || targetType == typeof(bool?))
            {
                bool b;
                if (bool.TryParse(Value.ToString(), out b))
                    return b;
                return false;
            }

            if (IsNumericType(targetType))
            {
                try
                {
                    return Convert.ChangeType(Value, targetType, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return 0;
                }
            }

            if (targetType.IsEnum)
            {
                try
                {
                    return Enum.Parse(targetType, Value.ToString());
                }
                catch
                {
                    return Activator.CreateInstance(targetType);
                }
            }

            return Value.ToString();
        }

        private static bool IsNumericType(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            if (t.IsPrimitive)
            {
                return t != typeof(bool) && t != typeof(char) && t != typeof(IntPtr) && t != typeof(UIntPtr);
            }
            return t == typeof(decimal);
        }
    }

    public class JArray : JToken, IEnumerable<JToken>
    {
        private readonly List<JToken> _items = new List<JToken>();

        public void Add(JToken item)
        {
            item.Parent = this;
            _items.Add(item);
        }

        public JToken this[int index]
        {
            get => _items[index];
            set
            {
                value.Parent = this;
                _items[index] = value;
            }
        }

        public int Count => _items.Count;

        public IEnumerator<JToken> GetEnumerator() => _items.GetEnumerator();
        System.Collections.IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < _items.Count; i++)
            {
                sb.Append(_items[i].ToString());
                if (i < _items.Count - 1)
                    sb.Append(",");
            }
            sb.Append("]");
            return sb.ToString();
        }

        internal override object ToObject(Type targetType)
        {
            if (targetType.IsArray)
            {
                var elementType = targetType.GetElementType();
                var arr = Array.CreateInstance(elementType, _items.Count);
                for (int i = 0; i < _items.Count; i++)
                {
                    arr.SetValue(_items[i].ToObject(elementType), i);
                }
                return arr;
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type t = targetType.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(targetType);

                foreach (var item in _items)
                {
                    list.Add(item.ToObject(t));
                }
                return list;
            }

            var resultList = new List<object>();
            foreach (var token in _items)
            {
                resultList.Add(token.ToObject(typeof(object)));
            }
            return resultList;
        }
    }

    public class JObject : JToken, IEnumerable<KeyValuePair<string, JToken>>
    {
        private readonly Dictionary<string, JToken> _properties = new Dictionary<string, JToken>(StringComparer.Ordinal);

        public JToken this[string key]
        {
            get
            {
                return _properties.TryGetValue(key, out var val) ? val : null;
            }
            set
            {
                value.Parent = this;
                _properties[key] = value;
            }
        }

        public IEnumerable<string> Keys => _properties.Keys;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            int count = _properties.Count;
            int i = 0;
            foreach (var kvp in _properties)
            {
                sb.Append("\"").Append(MyJsonConvert.Escape(kvp.Key)).Append("\":");
                sb.Append(kvp.Value.ToString());
                if (i < count - 1) sb.Append(",");
                i++;
            }
            sb.Append("}");
            return sb.ToString();
        }

        internal override object ToObject(Type targetType)
        {
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var args = targetType.GetGenericArguments();
                Type keyType = args[0];
                Type valType = args[1];
                if (keyType != typeof(string))
                {
                    return Activator.CreateInstance(targetType);
                }
                var dict = (IDictionary)Activator.CreateInstance(targetType);
                foreach (var kvp in _properties)
                {
                    var valObj = kvp.Value.ToObject(valType);
                    dict[kvp.Key] = valObj;
                }
                return dict;
            }

            var obj = Activator.CreateInstance(targetType);
            var props = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var fields = targetType.GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                if (!prop.CanWrite) continue;
                if (_properties.TryGetValue(prop.Name, out var jTok))
                {
                    var val = jTok.ToObject(prop.PropertyType);
                    prop.SetValue(obj, val);
                }
            }
            foreach (var field in fields)
            {
                if (_properties.TryGetValue(field.Name, out var jTok))
                {
                    var val = jTok.ToObject(field.FieldType);
                    field.SetValue(obj, val);
                }
            }

            return obj;
        }

        public IEnumerator<KeyValuePair<string, JToken>> GetEnumerator()
        {
            return _properties.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _properties.GetEnumerator();
        }
    }

    public static class MyJsonConvert
    {
        public static string SerializeObject(object obj)
        {
            var token = ObjectToJToken(obj);
            return token.ToString();
        }

        public static T DeserializeObject<T>(string json)
        {
            var token = Parse(json);
            return token.ToObject<T>();
        }

        public static JToken ObjectToJToken(object obj)
        {
            if (obj == null) return new JValue(null);

            Type t = obj.GetType();
            if (t == typeof(string) || t.IsPrimitive || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(Guid))
            {
                return new JValue(obj);
            }

            if (t.IsEnum)
            {
                return new JValue(obj.ToString());
            }

            if (typeof(IEnumerable).IsAssignableFrom(t) && t != typeof(string))
            {
                var arr = new JArray();
                foreach (var el in (IEnumerable)obj)
                {
                    arr.Add(ObjectToJToken(el));
                }
                return arr;
            }

            if (IsDictionaryStringKey(t))
            {
                var jObj = new JObject();
                var dict = (IDictionary)obj;
                foreach (var key in dict.Keys)
                {
                    string skey = key.ToString();
                    jObj[skey] = ObjectToJToken(dict[key]);
                }
                return jObj;
            }

            {
                var jObj = new JObject();

                var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var p in props)
                {
                    if (!p.CanRead) continue;
                    var val = p.GetValue(obj, null);
                    jObj[p.Name] = ObjectToJToken(val);
                }

                var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var f in fields)
                {
                    var val = f.GetValue(obj);
                    jObj[f.Name] = ObjectToJToken(val);
                }

                return jObj;
            }
        }

        public static JToken Parse(string json)
        {
            int index = 0;
            return ParseValue(json, ref index);
        }

        #region Internal Parser

        private static JToken ParseValue(string s, ref int i)
        {
            SkipWhiteSpace(s, ref i);

            if (i >= s.Length)
                return new JValue(null);

            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);

            if (char.IsDigit(c) || c == '-')
                return ParseNumber(s, ref i);

            if (i + 4 <= s.Length && s.Substring(i, 4).Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                i += 4;
                return new JValue(true);
            }
            if (i + 5 <= s.Length && s.Substring(i, 5).Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                i += 5;
                return new JValue(false);
            }
            if (i + 4 <= s.Length && s.Substring(i, 4).Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                i += 4;
                return new JValue(null);
            }

            return ParseString(s, ref i);
        }

        private static JToken ParseObject(string s, ref int i)
        {
            var obj = new JObject();
            i++;
            SkipWhiteSpace(s, ref i);

            bool first = true;
            while (i < s.Length)
            {
                SkipWhiteSpace(s, ref i);
                if (i < s.Length && s[i] == '}')
                {
                    i++;
                    break;
                }

                if (!first)
                {
                    if (i < s.Length && s[i] == ',')
                    {
                        i++;
                        SkipWhiteSpace(s, ref i);
                    }
                }
                first = false;

                string key = ParseJsonString(s, ref i);

                SkipWhiteSpace(s, ref i);
                if (i < s.Length && s[i] == ':')
                {
                    i++;
                    SkipWhiteSpace(s, ref i);
                    var val = ParseValue(s, ref i);
                    obj[key] = val;
                }
            }
            return obj;
        }

        private static JToken ParseArray(string s, ref int i)
        {
            var arr = new JArray();
            i++;
            SkipWhiteSpace(s, ref i);

            bool first = true;
            while (i < s.Length)
            {
                SkipWhiteSpace(s, ref i);
                if (i < s.Length && s[i] == ']')
                {
                    i++;
                    break;
                }
                if (!first)
                {
                    if (i < s.Length && s[i] == ',')
                    {
                        i++;
                        SkipWhiteSpace(s, ref i);
                    }
                }
                first = false;

                var val = ParseValue(s, ref i);
                arr.Add(val);
            }
            return arr;
        }

        private static JToken ParseString(string s, ref int i)
        {
            string str = ParseJsonString(s, ref i);
            return new JValue(str);
        }

        private static string ParseJsonString(string s, ref int i)
        {
            SkipWhiteSpace(s, ref i);
            if (i >= s.Length) return "";
            if (s[i] == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') break;
                    if (c == '\\')
                    {
                        if (i < s.Length)
                        {
                            char escaped = s[i++];
                            switch (escaped)
                            {
                                case 'n': sb.Append('\n'); break;
                                case 'r': sb.Append('\r'); break;
                                case 't': sb.Append('\t'); break;
                                case '\\': sb.Append('\\'); break;
                                case '"': sb.Append('"'); break;
                                default: sb.Append(escaped); break;
                            }
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                return sb.ToString();
            }
            else
            {
                var sb = new StringBuilder();
                while (i < s.Length)
                {
                    char c = s[i];
                    if (c == ',' || c == '}' || c == ']' || char.IsWhiteSpace(c))
                        break;
                    sb.Append(c);
                    i++;
                }
                return sb.ToString();
            }
        }

        private static JToken ParseNumber(string s, ref int i)
        {
            int start = i;
            bool hasDot = false;
            if (s[i] == '-') i++;

            while (i < s.Length)
            {
                char c = s[i];
                if (c == '.')
                {
                    if (hasDot) break;
                    hasDot = true;
                }
                else if (!char.IsDigit(c))
                    break;
                i++;
            }
            string numberStr = s.Substring(start, i - start);
            if (hasDot || numberStr.Contains("."))
            {
                if (float.TryParse(numberStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float fVal))
                {
                    return new JValue(fVal);
                }
                return new JValue(0f);
            }
            else
            {
                if (long.TryParse(numberStr, NumberStyles.Any, CultureInfo.InvariantCulture, out long lVal))
                {
                    return new JValue(lVal);
                }
                return new JValue(0);
            }
        }

        private static void SkipWhiteSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        #endregion

        internal static bool IsDictionaryStringKey(Type t)
        {
            if (!typeof(IDictionary).IsAssignableFrom(t))
                return false;
            return true;
        }

        internal static string SerializePrimitive(object value)
        {
            if (value == null) return "null";

            if (value is string)
            {
                return "\"" + Escape((string)value) + "\"";
            }
            if (value is bool b)
            {
                return b ? "true" : "false";
            }
            if (value is char c)
            {
                return "\"" + Escape(c.ToString()) + "\"";
            }
            if (value is DateTime dt)
            {
                return "\"" + Escape(dt.ToString("o", CultureInfo.InvariantCulture)) + "\"";
            }

            Type t = value.GetType();
            if (IsNumericType(t))
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return "\"" + Escape(value.ToString()) + "\"";
        }

        private static bool IsNumericType(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            if (t.IsPrimitive)
            {
                return t != typeof(bool) && t != typeof(char) && t != typeof(IntPtr) && t != typeof(UIntPtr);
            }
            return t == typeof(decimal);
        }
        internal static string Escape(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
