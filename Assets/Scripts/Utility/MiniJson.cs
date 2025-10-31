// MiniJson.cs
// Basit JSON serileþtirici/ayrýþtýrýcý (Unity uyumlu)
// MIT lisanslý MiniJSON'dan uyarlanmýþtýr.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public static class MiniJson
{
    public static object Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        return Parser.Parse(json);
    }

    public static string Serialize(object obj)
    {
        return Serializer.Serialize(obj);
    }

    // ---------------- Parser ----------------
    sealed class Parser : IDisposable
    {
        const string WORD_BREAK = "{}[],:\"";

        StringReader json;

        Parser(string jsonString)
        {
            json = new StringReader(jsonString);
        }

        public static object Parse(string jsonString)
        {
            using (var instance = new Parser(jsonString))
            {
                return instance.ParseValue();
            }
        }

        public void Dispose()
        {
            json.Dispose();
            json = null;
        }

        Dictionary<string, object> ParseObject()
        {
            var table = new Dictionary<string, object>();
            // {
            json.Read();

            // }
            while (true)
            {
                switch (NextToken)
                {
                    case TOKEN.NONE:
                        return null;
                    case TOKEN.CURLY_CLOSE:
                        json.Read();
                        return table;
                    default:
                        // key
                        string name = ParseString();
                        if (name == null) return null;

                        // :
                        if (NextToken != TOKEN.COLON) return null;
                        json.Read();

                        // value
                        table[name] = ParseValue();
                        break;
                }
            }
        }

        List<object> ParseArray()
        {
            var array = new List<object>();
            // [
            json.Read();

            var parsing = true;
            while (parsing)
            {
                TOKEN nextToken = NextToken;
                switch (nextToken)
                {
                    case TOKEN.NONE:
                        return null;
                    case TOKEN.SQUARE_CLOSE:
                        json.Read();
                        return array;
                    default:
                        array.Add(ParseValue());
                        break;
                }
            }
            return array;
        }

        object ParseValue()
        {
            switch (NextToken)
            {
                case TOKEN.STRING:
                    return ParseString();
                case TOKEN.NUMBER:
                    return ParseNumber();
                case TOKEN.CURLY_OPEN:
                    return ParseObject();
                case TOKEN.SQUARE_OPEN:
                    return ParseArray();
                case TOKEN.TRUE:
                    json.Read(); json.Read(); json.Read(); json.Read();
                    return true;
                case TOKEN.FALSE:
                    json.Read(); json.Read(); json.Read(); json.Read(); json.Read();
                    return false;
                case TOKEN.NULL:
                    json.Read(); json.Read(); json.Read(); json.Read();
                    return null;
                case TOKEN.NONE:
                default:
                    return null;
            }
        }

        string ParseString()
        {
            var s = new StringBuilder();
            char c;

            // "
            json.Read();

            bool parsing = true;
            while (parsing)
            {
                if (json.Peek() == -1) break;

                c = NextChar;
                switch (c)
                {
                    case '"':
                        parsing = false;
                        break;
                    case '\\':
                        if (json.Peek() == -1) { parsing = false; break; }
                        c = NextChar;
                        switch (c)
                        {
                            case '"': s.Append('"'); break;
                            case '\\': s.Append('\\'); break;
                            case '/': s.Append('/'); break;
                            case 'b': s.Append('\b'); break;
                            case 'f': s.Append('\f'); break;
                            case 'n': s.Append('\n'); break;
                            case 'r': s.Append('\r'); break;
                            case 't': s.Append('\t'); break;
                            case 'u':
                                var hex = new char[4];
                                for (int i = 0; i < 4; i++) hex[i] = NextChar;
                                s.Append((char)Convert.ToInt32(new string(hex), 16));
                                break;
                        }
                        break;
                    default:
                        s.Append(c);
                        break;
                }
            }
            return s.ToString();
        }

        object ParseNumber()
        {
            string number = NextWord;
            if (number.IndexOf('.', StringComparison.Ordinal) != -1 ||
                number.IndexOf('e', StringComparison.OrdinalIgnoreCase) != -1)
            {
                if (double.TryParse(number, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var d))
                    return d;
            }
            else
            {
                if (long.TryParse(number, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var l))
                    return l;
            }
            return 0;
        }

        void EatWhitespace()
        {
            while (json.Peek() != -1)
            {
                char c = (char)json.Peek();
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') { json.Read(); }
                else break;
            }
        }

        char NextChar => (char)json.Read();

        string NextWord
        {
            get
            {
                var sb = new StringBuilder();
                while (json.Peek() != -1 && WORD_BREAK.IndexOf((char)json.Peek()) == -1)
                {
                    sb.Append(NextChar);
                }
                return sb.ToString();
            }
        }

        TOKEN NextToken
        {
            get
            {
                EatWhitespace();
                if (json.Peek() == -1) return TOKEN.NONE;

                char c = (char)json.Peek();
                switch (c)
                {
                    case '{': return TOKEN.CURLY_OPEN;
                    case '}': return TOKEN.CURLY_CLOSE;
                    case '[': return TOKEN.SQUARE_OPEN;
                    case ']': return TOKEN.SQUARE_CLOSE;
                    case ',': json.Read(); return NextToken;
                    case '"': return TOKEN.STRING;
                    case ':': return TOKEN.COLON;
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                    case '-': return TOKEN.NUMBER;
                }

                string word = NextWord;
                switch (word)
                {
                    case "false": return TOKEN.FALSE;
                    case "true": return TOKEN.TRUE;
                    case "null": return TOKEN.NULL;
                }
                return TOKEN.NONE;
            }
        }

        enum TOKEN
        {
            NONE,
            CURLY_OPEN,
            CURLY_CLOSE,
            SQUARE_OPEN,
            SQUARE_CLOSE,
            COLON,
            COMMA,
            STRING,
            NUMBER,
            TRUE,
            FALSE,
            NULL
        }

        // Basit StringReader (GC allocs düþük olsun diye)
        sealed class StringReader : IDisposable
        {
            readonly string s;
            int i;
            public StringReader(string str) { s = str; i = 0; }
            public int Peek() => i < s.Length ? s[i] : -1;
            public int Read() => i < s.Length ? s[i++] : -1;
            public void Dispose() { }
        }
    }

    // ---------------- Serializer ----------------
    sealed class Serializer
    {
        StringBuilder builder;

        Serializer() { builder = new StringBuilder(); }

        public static string Serialize(object obj)
        {
            var instance = new Serializer();
            instance.SerializeValue(obj);
            return instance.builder.ToString();
        }

        void SerializeValue(object value)
        {
            if (value == null) { builder.Append("null"); return; }

            if (value is string s) { SerializeString(s); return; }
            if (value is bool b) { builder.Append(b ? "true" : "false"); return; }

            if (value is IDictionary dict) { SerializeObject(dict); return; }
            if (value is IDictionary<string, int> dictSI) { SerializeObject(dictSI); return; }
            if (value is IEnumerable enumerable) { SerializeArray(enumerable); return; }

            if (value is char c) { SerializeString(c.ToString()); return; }

            // sayýlar
            if (value is IFormattable fmt)
            {
                builder.Append(fmt.ToString(null, System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            // fallback: ToString
            SerializeString(value.ToString());
        }

        void SerializeObject(IDictionary obj)
        {
            bool first = true;
            builder.Append('{');

            foreach (var key in obj.Keys)
            {
                if (!first) builder.Append(',');
                SerializeString(key.ToString());
                builder.Append(':');
                SerializeValue(obj[key]);
                first = false;
            }

            builder.Append('}');
        }

        void SerializeObject<TKey, TValue>(IDictionary<TKey, TValue> obj)
        {
            bool first = true;
            builder.Append('{');

            foreach (var kv in obj)
            {
                if (!first) builder.Append(',');
                SerializeString(kv.Key.ToString());
                builder.Append(':');
                SerializeValue(kv.Value);
                first = false;
            }

            builder.Append('}');
        }

        void SerializeArray(IEnumerable array)
        {
            builder.Append('[');
            bool first = true;
            foreach (var obj in array)
            {
                if (!first) builder.Append(',');
                SerializeValue(obj);
                first = false;
            }
            builder.Append(']');
        }

        void SerializeString(string str)
        {
            builder.Append('\"');
            foreach (var c in str)
            {
                switch (c)
                {
                    case '\"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < ' ' || c > 0x7E)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4"));
                        }
                        else builder.Append(c);
                        break;
                }
            }
            builder.Append('\"');
        }
    }
}
