using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AcgJson
{
    public class Deserializer : IDisposable
    {
        #region base

        public Deserializer(string json)
        {
            this.json = new StringReader(json);
            this.skipItemFunc = SkipItem;
        }

        StringReader json;
        int pointer;
        Func<object> skipItemFunc;
        StringBuilder stringBuilder = new StringBuilder();

        int Peek { get { return json.Peek(); } }
        void Next() { pointer++; json.Read(); }

        public static Action<string, int> NotifyErrorFunc = (message, pointer) => { throw new Exception(string.Format("[Pointer:{0}] {1}", pointer, message)); };
        void NotifyError(string message) { NotifyErrorFunc.Invoke(message, pointer); }
        void NotifyError(IEnumerable<string> expectedValue) { NotifyError("expect " + expectedValue.Aggregate((s1, s2) => string.Format("{0}, {1}", s1, s2))); }

        void IDisposable.Dispose()
        {
            json.Dispose();
            json = null;
        }

        #endregion

        #region const

        const int EndOfStream = -1;
        const int StringEdge = '"';
        const int PairsHead = '{';
        const int PairsTail = '}';
        const int CollectionHead = '[';
        const int CollectionTail = ']';
        const int PairConnector = ':';
        const int ItemSeparator = ',';
        static readonly HashSet<int> WhiteSpaces = new HashSet<int>()
        {
            '\u0020', '\u1680', '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007',
            '\u2008', '\u2009', '\u200a', '\u202f', '\u205f', '\u3000',
            '\u2028',
            '\u2029',
            '\u0009', '\u000a', '\u000b', '\u000c', '\u000d', '\u0085', '\u00a0',
        };
        static readonly HashSet<int> ControlCodes = new HashSet<int>()
        {
            EndOfStream,
            StringEdge,
            PairsHead,
            PairsTail,
            CollectionHead,
            CollectionTail,
            PairConnector,
            ItemSeparator,
        };
        static readonly HashSet<int> NonRawChars = new HashSet<int>(WhiteSpaces.Concat(ControlCodes));
        static readonly Dictionary<int, char> EscapedPairs = new Dictionary<int, char>()
        {
            { '"', '"' }, { '\\', '\\' }, { '/', '/' },
            { 'b', '\b' }, { 'f', '\f' }, { 'n', '\n' }, { 'r', '\r' }, { 't', '\t' },
            { 'B', '\b' }, { 'F', '\f' }, { 'N', '\n' }, { 'R', '\r' }, { 'T', '\t' },
        };
        static readonly HashSet<int> UnicodeHeads = new HashSet<int>() { 'u', 'U' };
        static readonly Dictionary<int, int> HexadecimalPairs = new Dictionary<int, int>()
        {
            { '0', 0 }, { '1', 1 }, { '2', 2 }, { '3', 3 }, { '4', 4 },
            { '5', 5 }, { '6', 6 }, { '7', 7 }, { '8', 8 }, { '9', 9 },
            { 'a', 10 }, { 'b', 11 }, { 'c', 12 }, { 'd', 13 }, { 'e', 14 }, { 'f', 15 },
            { 'A', 10 }, { 'B', 11 }, { 'C', 12 }, { 'D', 13 }, { 'E', 14 }, { 'F', 15 },
        };

        const char PlusSignChar = '+';
        const char MinusSignChar = '-';
        const char DecimalPointChar = '.';
        static readonly HashSet<char> ExponentChars = new HashSet<char>() { 'e', 'E' };
        static readonly HashSet<char> IntegralParseTerminationChars = new HashSet<char>() { '.', 'e', 'E' };
        const ulong IntegralBase = 10UL;
        const double DecimalBase = 10d;
        static readonly Dictionary<char, ulong> DigitIntegralPairs = new Dictionary<char, ulong>()
        {
            { '0', 0UL }, { '1', 1UL }, { '2', 2UL }, { '3', 3UL }, { '4', 4UL },
            { '5', 5UL }, { '6', 6UL }, { '7', 7UL }, { '8', 8UL }, { '9', 9UL },
        };
        static readonly Dictionary<char, double> DigitDecimalPairs = new Dictionary<char, double>()
        {
            { '0', 0d }, { '1', 1d }, { '2', 2d }, { '3', 3d }, { '4', 4d },
            { '5', 5d }, { '6', 6d }, { '7', 7d }, { '8', 8d }, { '9', 9d },
        };

        const string NullStringSmall = "null";
        const string NullStringLarge = "NULL";
        const string TrueStringSmall = "true";
        const string TrueStringLarge = "TRUE";
        const string TrueStringNumber = "1";
        const string FalseStringSmall = "false";
        const string FalseStringLarge = "FALSE";
        const string FalseStringNumber = "0";
        static readonly Dictionary<char, KeyValuePair<string, bool?>> NullableBoolTable = new Dictionary<char, KeyValuePair<string, bool?>>()
        {
            { NullStringSmall[0], new KeyValuePair<string, bool?>(NullStringSmall, null) },
            { NullStringLarge[0], new KeyValuePair<string, bool?>(NullStringLarge, null) },
            { TrueStringSmall[0], new KeyValuePair<string, bool?>(TrueStringSmall, true) },
            { TrueStringLarge[0], new KeyValuePair<string, bool?>(TrueStringLarge, true) },
            { TrueStringNumber[0], new KeyValuePair<string, bool?>(TrueStringNumber, true) },
            { FalseStringSmall[0], new KeyValuePair<string, bool?>(FalseStringSmall, false) },
            { FalseStringLarge[0], new KeyValuePair<string, bool?>(FalseStringLarge, false) },
            { FalseStringNumber[0], new KeyValuePair<string, bool?>(FalseStringNumber, false) },
        };
        static readonly Dictionary<char, KeyValuePair<string, bool>> BoolTable = new Dictionary<char, KeyValuePair<string, bool>>()
        {
            { TrueStringSmall[0], new KeyValuePair<string, bool>(TrueStringSmall, true) },
            { TrueStringLarge[0], new KeyValuePair<string, bool>(TrueStringLarge, true) },
            { TrueStringNumber[0], new KeyValuePair<string, bool>(TrueStringNumber, true) },
            { FalseStringSmall[0], new KeyValuePair<string, bool>(FalseStringSmall, false) },
            { FalseStringLarge[0], new KeyValuePair<string, bool>(FalseStringLarge, false) },
            { FalseStringNumber[0], new KeyValuePair<string, bool>(FalseStringNumber, false) },
        };
        static readonly Dictionary<char, KeyValuePair<string, object>> NullTable = new Dictionary<char, KeyValuePair<string, object>>()
        {
            { NullStringSmall[0], new KeyValuePair<string, object>(NullStringSmall, null) },
            { NullStringLarge[0], new KeyValuePair<string, object>(NullStringLarge, null) },
        };

        #endregion

        #region array, list

        public List<T> GetList<T>(Func<T> getValueFunc)
        {
            var enumerable = GetCollection(getValueFunc);
            if (enumerable != null)
                return enumerable.ToList();
            else
                return null;
        }

        public T[] GetArray<T>(Func<T> getValueFunc)
        {
            var enumerable = GetCollection(getValueFunc);
            if (enumerable != null)
                return enumerable.ToArray();
            else
                return null;
        }

        IEnumerable<T> GetCollection<T>(Func<T> getValueFunc)
        {
            SkipWhiteSpaces();
            if (Peek == CollectionHead)
                return EnumerateCollection(getValueFunc);
            else
            {
                SkipItemAndCheckNull();
                return null;
            }
        }

        IEnumerable<T> EnumerateCollection<T>(Func<T> getValueFunc)
        {
            // remove '['
            Next();

            SkipWhiteSpaces();

            // empty
            if (Peek == CollectionTail)
            {
                // remove ']'
                Next();
                yield break;
            }

            while (true)
            {
                if (Peek == EndOfStream)
                {
                    NotifyError("unexpected end of string");
                    yield break;
                }

                yield return getValueFunc.Invoke();

                SkipWhiteSpaces();

                if (Peek == ItemSeparator)
                {
                    // remove ','
                    Next();
                }
                else if (Peek == CollectionTail)
                {
                    // remove ']'
                    Next();
                    break;
                }
                else
                {
                    NotifyError("expect value separator: " + Peek);
                    yield break;
                }
            }
        }

        #endregion

        #region dictionary

        public Dictionary<TKey, TValue> GetDictionary<TKey, TValue>(Func<TKey> getKeyFunc, Func<TValue> getValueFunc)
        {
            var enumerable = GetPairs(getKeyFunc, getValueFunc);
            if (enumerable != null)
                return enumerable.ToDictionary(pair => pair.Key, pair => pair.Value);
            else
                return null;
        }

        IEnumerable<KeyValuePair<TKey, TValue>> GetPairs<TKey, TValue>(Func<TKey> getKeyFunc, Func<TValue> getValueFunc)
        {
            SkipWhiteSpaces();
            if (Peek == PairsHead)
                return EnumeratePairs(getKeyFunc, getValueFunc);
            else
            {
                SkipItemAndCheckNull();
                return null;
            }
        }

        IEnumerable<KeyValuePair<TKey, TValue>> EnumeratePairs<TKey, TValue>(Func<TKey> getKeyFunc, Func<TValue> getValueFunc)
        {
            // remove '{'
            Next();

            SkipWhiteSpaces();

            // empty
            if (Peek == PairsTail)
            {
                // remove '}'
                Next();
                yield break;
            }

            while (true)
            {
                if (Peek == EndOfStream)
                {
                    NotifyError("unexpected end of string");
                    yield break;
                }

                var key = getKeyFunc.Invoke();

                SkipWhiteSpaces();

                if (Peek != PairConnector)
                {
                    NotifyError("expect pair connector: " + Peek);
                    yield break;
                }

                // remove ':'
                Next();

                var value = getValueFunc.Invoke();

                yield return new KeyValuePair<TKey, TValue>(key, value);

                if (Peek == ItemSeparator)
                {
                    // remove ','
                    Next();
                }
                else if (Peek == PairsTail)
                {
                    // remove '}'
                    Next();
                    break;
                }
                else
                {
                    NotifyError("expect value separator: " + Peek);
                    yield break;
                }
            }
        }

        #endregion

        #region object

        public void DeserializeObject<TSetter>(TSetter obj, Dictionary<string, Action<TSetter, Deserializer>> deserializeActions)
        {
            // remove '{'
            Next();

            SkipWhiteSpaces();

            // empty
            if (Peek == PairsTail)
            {
                // remove '}'
                Next();
                return;
            }

            while (true)
            {
                if (Peek == EndOfStream)
                {
                    NotifyError("unexpected end of string");
                    return;
                }

                var key = GetString();

                SkipWhiteSpaces();

                if (Peek != PairConnector)
                {
                    NotifyError("expect pair connector: " + Peek);
                    return;
                }

                // remove ':'
                Next();

                Action<TSetter, Deserializer> deserializeAction;
                if (deserializeActions.TryGetValue(key, out deserializeAction))
                    deserializeAction.Invoke(obj, this);
                else
                    SkipItem();

                SkipWhiteSpaces();

                if (Peek == ItemSeparator)
                {
                    // remove ','
                    Next();
                }
                else if (Peek == PairsTail)
                {
                    // remove '}'
                    Next();
                    break;
                }
                else
                {
                    NotifyError("expect value separator: " + Peek);
                    return;
                }
            }
        }

        public T GetObject<T>() where T : IDeserializable, new()
        {
            SkipWhiteSpaces();

            if (Peek == PairsHead)
            {
                var result = new T();
                result.Deserialize(this);
                return result;
            }
            else
            {
                SkipItemAndCheckNull();
                return default(T);
            }
        }

        #endregion

        #region boolean

        public string GetString()
        {
            IEnumerable<char> chars;
            if (TryGetEnclosedChars(out chars))
                return ConvertCharsToString(chars);

            if (TryGetRawChars(out chars))
            {
                var str = ConvertCharsToString(chars);
                // NullCheck
                return str;
            }

            NotifyError("unexpect string: " + Peek);
            SkipItem();
            return null;
        }

        string ConvertCharsToString(IEnumerable<char> chars)
        {
            stringBuilder.Length = 0;

            foreach (var c in chars)
                stringBuilder.Append(c);

            return stringBuilder.ToString();

        }

        public bool GetBool()
        {
            return GetItemFromTable(BoolTable);
        }

        public bool? GetNullableBool()
        {
            return GetItemFromTable(NullableBoolTable);
        }

        T GetItemFromTable<T>(Dictionary<char, KeyValuePair<string, T>> table)
        {
            T result;
            IEnumerator<char> charsEnumerator;
            if (!TryGetItemFromTable(table, out result, out charsEnumerator))
            {
                if (charsEnumerator != null)
                {
                    NotifyError(table.Select(p => p.Value.Key));
                    ConsumeEnumerator(charsEnumerator);
                }
            }

            return result;
        }

        bool TryGetItemFromTable<T>(Dictionary<char, KeyValuePair<string, T>> table, out T result, out IEnumerator<char> charsEnumerator)
        {
            IEnumerable<char> chars;
            if (!TryGetChars(out chars))
            {
                NotifyError(table.Select(p => p.Value.Key));
                SkipItem();
                result = default(T);
                charsEnumerator = null;
                return false;
            }

            var trialEnumerator = chars.GetEnumerator();
            trialEnumerator.MoveNext();

            KeyValuePair<string, T> stringItemPair;
            if (!table.TryGetValue(trialEnumerator.Current, out stringItemPair))
            {
                charsEnumerator = trialEnumerator;
                result = default(T);
                return false;
            }

            string str = stringItemPair.Key;
            var correctEnumerator = str.GetEnumerator();
            correctEnumerator.MoveNext();

            if (!AreSameCharsEnumerators(trialEnumerator, correctEnumerator))
            {
                NotifyError(table.Select(p => p.Value.Key));
                ConsumeEnumerator(trialEnumerator);
                result = default(T);
                charsEnumerator = null;
                return false;
            }

            result = stringItemPair.Value;
            charsEnumerator = null;
            return true;
        }

        bool AreSameCharsEnumerators(IEnumerator<char> chars1, IEnumerator<char> chars2)
        {
            while (true)
            {
                if (chars1.Current != chars2.Current)
                    return false;

                bool next1 = chars1.MoveNext();
                bool next2 = chars2.MoveNext();

                if (!next1 && !next2)
                    return true;
                else if (next1 != next2)
                    return false;
            }
        }

        #endregion

        #region integral number

        public ulong? GetNullableUnsignedLong()
        {
            object obj;
            IEnumerator<char> charsEnumerator;
            if (TryGetItemFromTable(NullTable, out obj, out charsEnumerator))
                return null;

            if (charsEnumerator == null)
                return null;

            return ParseToUnsignedLong(charsEnumerator);
        }

        public ulong GetUnsignedLong()
        {
            IEnumerable<char> chars;
            if (!TryGetChars(out chars))
            {
                NotifyError("expect integral number");
                SkipItem();
                return default(ulong);
            }

            var charsEnumerator = chars.GetEnumerator();
            charsEnumerator.MoveNext();

            return ParseToUnsignedLong(charsEnumerator);
        }

        ulong ParseToUnsignedLong(IEnumerator<char> charsEnumerator)
        {
            ulong magnitude;
            bool isPlus;
            if (!TryParseToUnsignedLong(charsEnumerator, out magnitude, out isPlus))
            {
                NotifyError("expect integral number");
                ConsumeEnumerator(charsEnumerator);
                return default(long);
            }

            if (!isPlus)
            {
                NotifyError("expect plus integral number");
                return default(ulong);
            }

            return magnitude;
        }

        public long? GetNullableLong()
        {
            object result;
            IEnumerator<char> charsEnumerator;
            if (TryGetItemFromTable(NullTable, out result, out charsEnumerator))
                return null;

            if (charsEnumerator == null)
                return null;

            return ParseToLong(charsEnumerator);
        }

        public long GetLong()
        {
            IEnumerable<char> chars;
            if (!TryGetChars(out chars))
            {
                NotifyError("expect integral number");
                SkipItem();
                return default(long);
            }

            var charsEnumerator = chars.GetEnumerator();
            charsEnumerator.MoveNext();

            return ParseToLong(charsEnumerator);
        }

        long ParseToLong(IEnumerator<char> charsEnumerator)
        {
            ulong magnitude;
            bool isPlus;
            if (!TryParseToUnsignedLong(charsEnumerator, out magnitude, out isPlus))
            {
                NotifyError("expect integral number");
                ConsumeEnumerator(charsEnumerator);
                return default(long);
            }

            if (isPlus)
                return (long)magnitude;
            else
                return -1L * (long)magnitude;
        }

        bool TryParseToUnsignedLong(IEnumerator<char> charsEnumerator, out ulong magnitude, out bool isPlus)
        {
            switch (charsEnumerator.Current)
            {
                case MinusSignChar:
                    isPlus = false;
                    charsEnumerator.MoveNext();
                    break;
                case PlusSignChar:
                    isPlus = true;
                    charsEnumerator.MoveNext();
                    break;
                default:
                    isPlus = true;
                    break;
            }

            magnitude = 0UL;

            while (true)
            {
                ulong digit;
                if (DigitIntegralPairs.TryGetValue(charsEnumerator.Current, out digit))
                {
                    magnitude *= IntegralBase;
                    magnitude += digit;
                }
                else if (IntegralParseTerminationChars.Contains(charsEnumerator.Current))
                {
                    return false;
                }
                else
                {
                    NotifyError("expect number");
                    ConsumeEnumerator(charsEnumerator);
                    magnitude = default(ulong);
                    return true;
                }

                if (!charsEnumerator.MoveNext())
                    break;
            }

            return true;
        }

        public uint? GetNullableUnsignedInt()
        {
            return (uint?)GetNullableUnsignedLong();
        }

        public uint GetUnsignedInt()
        {
            return (uint)GetUnsignedLong();
        }

        public int? GetNullableInt()
        {
            return (int?)GetNullableLong();
        }

        public int GetInt()
        {
            return (int)GetLong();
        }

        public ushort? GetNullableUnsignedShort()
        {
            return (ushort?)GetNullableUnsignedLong();
        }

        public ushort GetUnsignedShort()
        {
            return (ushort)GetUnsignedLong();
        }

        public short? GetNullableShort()
        {
            return (short?)GetNullableLong();
        }

        public int GetShort()
        {
            return (short)GetLong();
        }

        public char? GetNullableChar()
        {
            return (char?)GetNullableUnsignedLong();
        }

        public char GetChar()
        {
            return (char)GetUnsignedLong();
        }

        public byte? GetNullableByte()
        {
            return (byte?)GetNullableUnsignedLong();
        }

        public byte GetByte()
        {
            return (byte)GetUnsignedLong();
        }

        public sbyte? GetNullableSignedByte()
        {
            return (sbyte?)GetNullableLong();
        }

        public sbyte GetSignedByte()
        {
            return (sbyte)GetLong();
        }

        #endregion

        #region decimal

        public double? GetNullableDouble()
        {
            object result;
            IEnumerator<char> charsEnumerator;
            if (TryGetItemFromTable(NullTable, out result, out charsEnumerator))
                return null;

            if (charsEnumerator == null)
                return null;

            return ParseToDouble(charsEnumerator);
        }

        public double GetDouble()
        {
            IEnumerable<char> chars;
            if (!TryGetChars(out chars))
            {
                NotifyError("expect decimal");
                SkipItem();
                return default(long);
            }

            var charsEnumerator = chars.GetEnumerator();
            charsEnumerator.MoveNext();

            return ParseToDouble(charsEnumerator);
        }

        double ParseToDouble(IEnumerator<char> charsEnumerator)
        {
            double result;
            bool isPlus;
            if (!TryParseToDouble(charsEnumerator, out result, out isPlus))
            {
                NotifyError("expect decimal");
                return default(double);
            }

            if (isPlus)
                return result;
            else
                return -1d * result;
        }

        bool TryParseToDouble(IEnumerator<char> charsEnumerator, out double result, out bool isPlusNumber)
        {
            ulong integralMagnitude;
            if (TryParseToUnsignedLong(charsEnumerator, out integralMagnitude, out isPlusNumber))
            {
                result = (double)integralMagnitude;
                return true;
            }

            double magnitude = (double)integralMagnitude;

            if (charsEnumerator.Current == DecimalPointChar)
            {
                // remove '.'
                if (!charsEnumerator.MoveNext())
                {
                    // unexpected end of stream
                    result = default(double);
                    return false;
                }

                double unit = 0.1d;

                while (true)
                {
                    double digit;
                    if (DigitDecimalPairs.TryGetValue(charsEnumerator.Current, out digit))
                    {
                        magnitude += digit * unit;
                    }
                    else if (ExponentChars.Contains(charsEnumerator.Current))
                    {
                        break;
                    }
                    else
                    {
                        // unexpected char
                        result = default(double);
                        return false;
                    }

                    if (!charsEnumerator.MoveNext())
                    {
                        result = magnitude;
                        return true;
                    }

                    unit /= DecimalBase;
                }
            }

            if (!ExponentChars.Contains(charsEnumerator.Current))
            {
                // unexpected char
                result = default(double);
                return false;
            }

            // remove '.'
            if (!charsEnumerator.MoveNext())
            {
                // unexpected end of stream
                result = default(double);
                return false;
            }

            bool isPlusExponent = false;
            switch (charsEnumerator.Current)
            {
                case MinusSignChar:
                    isPlusExponent = false;
                    break;
                case PlusSignChar:
                    isPlusExponent = true;
                    break;
                default:
                    // unexpected char
                    result = default(double);
                    return false;
            }

            // remove '+' or '-'
            if (!charsEnumerator.MoveNext())
            {
                // unexpected end of stream
                result = default(double);
                return false;
            }

            ulong exponent = 0UL;

            while (true)
            {
                ulong digit;
                if (DigitIntegralPairs.TryGetValue(charsEnumerator.Current, out digit))
                {
                    exponent *= IntegralBase;
                    exponent += digit;
                }
                else
                {
                    // unexpected char
                    result = default(double);
                    return false;
                }

                if (!charsEnumerator.MoveNext())
                    break;
            }

            if (isPlusExponent)
            {
                for (ulong i = 0; i < exponent; i++)
                    magnitude *= DecimalBase;
            }
            else
            {
                for (ulong i = 0; i < exponent; i++)
                    magnitude /= DecimalBase;
            }

            result = magnitude;
            return true;
        }

        public float? GetNullableFloat()
        {
            return (float?)GetNullableDouble();
        }

        public float GetFloat()
        {
            return (float)GetDouble();
        }

        #endregion

        #region utility

        void SkipWhiteSpaces()
        {
            while (WhiteSpaces.Contains(Peek))
                Next();
        }

        bool TryGetChars(out IEnumerable<char> chars)
        {
            if (TryGetEnclosedChars(out chars))
                return true;

            if (TryGetRawChars(out chars))
                return true;

            chars = null;
            return false;
        }

        bool TryGetEnclosedChars(out IEnumerable<char> chars)
        {
            SkipWhiteSpaces();
            if (Peek == StringEdge)
            {
                chars = EnumerateEnclosedChars();
                return true;
            }
            else
            {
                chars = null;
                return false;
            }
        }

        IEnumerable<char> EnumerateEnclosedChars()
        {
            // remove '"'
            Next();

            while (Peek != StringEdge)
            {
                if (Peek == EndOfStream)
                    yield break;

                if (Peek != '\\')
                {
                    yield return (char)Peek;
                    Next();
                }
                else
                {
                    // remove '\'
                    Next();
                    var nextChar = Peek;
                    char escapedChar = default(char);
                    if (EscapedPairs.TryGetValue(nextChar, out escapedChar))
                    {
                        yield return escapedChar;
                        Next();
                    }
                    else if (UnicodeHeads.Contains(nextChar))
                    {
                        // remove 'u' or 'U'
                        Next();

                        int decodedChar = 0;
                        for (int i = 0; i < 4; i++)
                        {
                            decodedChar *= 16;
                            decodedChar += ConvertCharToHexadecimal(Peek);
                            Next();
                        }

                        yield return (char)decodedChar;
                    }
                }
            }

            // remove '"'
            Next();
        }

        int ConvertCharToHexadecimal(int character)
        {
            int result;
            if (HexadecimalPairs.TryGetValue(character, out result))
                return result;

            NotifyError("expect hexadecimal character: " + character);
            return 0;
        }

        bool TryGetRawChars(out IEnumerable<char> chars)
        {
            SkipWhiteSpaces();
            if (!NonRawChars.Contains(Peek))
            {
                chars = EnumerateRawChars();
                return true;
            }
            else
            {
                chars = null;
                return false;
            }
        }

        IEnumerable<char> EnumerateRawChars()
        {
            while (!NonRawChars.Contains(Peek))
            {
                yield return (char)Peek;
                Next();
            }
        }

        IEnumerable<char> GetBlank()
        {
            yield break;
        }

        void SkipItemAndCheckNull()
        {
            GetItemFromTable(NullTable);
        }

        object SkipItem()
        {
            IEnumerable<char> chars;
            if (TryGetChars(out chars))
            {
                ConsumeEnumerable(chars);
                return null;
            }

            if (Peek == CollectionHead)
            {
                ConsumeEnumerable(GetCollection(skipItemFunc));
                return null;
            }

            if (Peek == PairsHead)
            {
                ConsumeEnumerable(GetPairs(skipItemFunc, skipItemFunc));
                return null;
            }

            NotifyError("unexpect item head: " + Peek);
            return null;
        }

        void ConsumeEnumerable<T>(IEnumerable<T> enumerable)
        {
            ConsumeEnumerator(enumerable.GetEnumerator());
        }

        void ConsumeEnumerator<T>(IEnumerator<T> enumerator)
        {
            while (enumerator.MoveNext()) { }
        }

        #endregion
    }

    public interface IDeserializable
    {
        void Deserialize(Deserializer deserializer);
    }
}
