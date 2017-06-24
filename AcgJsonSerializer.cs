using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AcgJson
{
    public static class AcgJsonSerializer
    {
        const string NullString = "null";
        const string TrueString = "true";
        const string FalseString = "false";
        const char StringEdge = '"';
        const char PairsHead = '{';
        const char PairsTail = '}';
        const char CollectionHead = '[';
        const char CollectionTail = ']';
        const char PairConnector = ':';
        const char ItemSeparator = ',';
        const char EscapeCode = '\\';
        static readonly Dictionary<char, char> EscapedPairs = new Dictionary<char, char>()
        {
            { '"', '"' }, { '\\', '\\' }, { '/', '/' },
            { '\b', 'b' }, { '\f', 'f' }, { '\n', 'n' }, { '\r', 'r' }, { '\t', 't' },
        };

        public static void AppendJson(this StringBuilder stringBuilder, ISerializable serializable)
        {
            serializable.AppendJson(stringBuilder);
        }

        public static void AppendJson(this StringBuilder stringBuilder, string str)
        {
            if (str == null)
            {
                stringBuilder.Append(NullString);
                return;
            }

            stringBuilder.Append(StringEdge);

            foreach (char c in str)
            {
                char escapedChar;
                if (EscapedPairs.TryGetValue(c, out escapedChar))
                {
                    stringBuilder.Append(EscapeCode);
                    stringBuilder.Append(escapedChar);
                }
                else
                {
                    stringBuilder.Append(c);
                }
            }

            stringBuilder.Append('"');
        }

        public static void AppendJson<T>(this StringBuilder stringBuilder, ICollection<T> collection, Action<StringBuilder, T> appendAction)
        {
            if (collection == null)
            {
                stringBuilder.Append(NullString);
                return;
            }

            stringBuilder.Append(CollectionHead);

            if (collection.Count <= 0)
            {
                stringBuilder.Append(CollectionTail);
                return;
            }

            var enumerator = collection.GetEnumerator();
            enumerator.MoveNext();

            while (true)
            {
                appendAction.Invoke(stringBuilder, enumerator.Current);

                if (!enumerator.MoveNext())
                    break;

                stringBuilder.Append(ItemSeparator);
            }

            stringBuilder.Append(CollectionTail);
        }

        public static void AppendJson<TKey, TValue>(this StringBuilder stringBuilder, IDictionary<TKey, TValue> dictionary, Action<StringBuilder, TKey> appendKeyAction, Action<StringBuilder, TValue> appendValueAction)
        {
            if (dictionary == null)
            {
                stringBuilder.Append(NullString);
                return;
            }

            stringBuilder.Append(PairsHead);

            if (dictionary.Count <= 0)
            {
                stringBuilder.Append(PairsTail);
                return;
            }

            var enumerator = dictionary.GetEnumerator();
            enumerator.MoveNext();

            while (true)
            {
                appendKeyAction.Invoke(stringBuilder, enumerator.Current.Key);
                stringBuilder.Append(PairConnector);
                appendValueAction.Invoke(stringBuilder, enumerator.Current.Value);

                if (!enumerator.MoveNext())
                    break;

                stringBuilder.Append(ItemSeparator);
            }

            stringBuilder.Append(PairsTail);
        }

        public static void AppendJson(this StringBuilder stringBuilder, bool? nullableBoolean)
        {
            stringBuilder.Append(
                !nullableBoolean.HasValue ? NullString :
                nullableBoolean.Value ? TrueString : FalseString);
        }

        public static void AppendJson(this StringBuilder stringBuilder, bool boolean)
        {
            stringBuilder.Append(boolean ? TrueString : FalseString);
        }

        public static void AppendJson(this StringBuilder stringBuilder, ulong? nullableNumber)
        {
            stringBuilder.Append(nullableNumber.HasValue ? nullableNumber.Value.ToString() : NullString);
        }

        public static void AppendJson(this StringBuilder stringBuilder, ulong number)
        {
            stringBuilder.Append(number.ToString());
        }

        public static void AppendJson(this StringBuilder stringBuilder, long? nullableNumber)
        {
            stringBuilder.Append(nullableNumber.HasValue ? nullableNumber.Value.ToString() : NullString);
        }

        public static void AppendJson(this StringBuilder stringBuilder, long number)
        {
            stringBuilder.Append(number.ToString());
        }

        public static void AppendJson(this StringBuilder stringBuilder, uint? nullableNumber)
        {
            stringBuilder.Append(nullableNumber.HasValue ? nullableNumber.Value.ToString() : NullString);
        }

        public static void AppendJson(this StringBuilder stringBuilder, uint number)
        {
            stringBuilder.Append(number.ToString());
        }

        public static void AppendJson(this StringBuilder stringBuilder, int? nullableNumber)
        {
            stringBuilder.Append(nullableNumber.HasValue ? nullableNumber.Value.ToString() : NullString);
        }

        public static void AppendJson(this StringBuilder stringBuilder, int number)
        {
            stringBuilder.Append(number.ToString());
        }

        public static void AppendJson(this StringBuilder stringBuilder, char? nullableNumber)
        {
            stringBuilder.Append(nullableNumber.HasValue ? nullableNumber.Value.ToString() : NullString);
        }

        public static void AppendJson(this StringBuilder stringBuilder, char number)
        {
            stringBuilder.Append(number.ToString());
        }

        public static void AppendJson(this StringBuilder stringBuilder, ushort? nullableNumber)
        {
            stringBuilder.Append(nullableNumber.HasValue ? nullableNumber.Value.ToString() : NullString);
        }

        public static void AppendJson(this StringBuilder stringBuilder, ushort number)
        {
            stringBuilder.Append(number.ToString());
        }

        public static void AppendJson(this StringBuilder stringBuilder, short? nullableNumber)
        {
            stringBuilder.Append(nullableNumber.HasValue ? nullableNumber.Value.ToString() : NullString);
        }

        public static void AppendJson(this StringBuilder stringBuilder, short number)
        {
            stringBuilder.Append(number.ToString());
        }

        public static void AppendJson(this StringBuilder stringBuilder, byte? nullableNumber)
        {
            stringBuilder.Append(nullableNumber.HasValue ? nullableNumber.Value.ToString() : NullString);
        }

        public static void AppendJson(this StringBuilder stringBuilder, byte number)
        {
            stringBuilder.Append(number.ToString());
        }

        public static void AppendJson(this StringBuilder stringBuilder, sbyte? nullableNumber)
        {
            stringBuilder.Append(nullableNumber.HasValue ? nullableNumber.Value.ToString() : NullString);
        }

        public static void AppendJson(this StringBuilder stringBuilder, sbyte number)
        {
            stringBuilder.Append(number.ToString());
        }

        public static void AppendJson(this StringBuilder stringBuilder, double? nullableNumber)
        {
            stringBuilder.Append(nullableNumber.HasValue ? nullableNumber.Value.ToString("g") : NullString);
        }

        public static void AppendJson(this StringBuilder stringBuilder, double number)
        {
            stringBuilder.Append(number.ToString("g"));
        }

        public static void AppendJson(this StringBuilder stringBuilder, float? nullableNumber)
        {
            stringBuilder.Append(nullableNumber.HasValue ? nullableNumber.Value.ToString("g") : NullString);
        }

        public static void AppendJson(this StringBuilder stringBuilder, float number)
        {
            stringBuilder.Append(number.ToString("g"));
        }
    }

    public interface ISerializable
    {
        void AppendJson(StringBuilder stringBuilder);
    }
}
