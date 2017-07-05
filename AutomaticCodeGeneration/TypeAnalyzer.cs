using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcgJson.AutomaticCodeGeneration
{
    public class TypeAnalyzer
    {
        public TypeAnalyzer()
        {
        }

        public IType Analyze(string type)
        {
            var tokens = Parse(type).GetEnumerator();
            tokens.MoveNext();

            return AnalyzeRecursively(tokens);
        }

        IType AnalyzeRecursively(IEnumerator<string> tokens)
        {
            var current = tokens.Current;

            switch (current)
            {
                case "Dictionary":
                    tokens.MoveNext();
                    CheckToken("<", tokens);
                    var keyType = AnalyzeRecursively(tokens);
                    CheckToken(",", tokens);
                    var valueType = AnalyzeRecursively(tokens);
                    CheckToken(">", tokens);
                    return new DictionaryType(keyType, valueType);
                case "List":
                    tokens.MoveNext();
                    CheckToken("<", tokens);
                    var elementType = AnalyzeRecursively(tokens);
                    CheckToken(">", tokens);
                    return new ListType(elementType);
                case "<":
                case ">":
                case "[":
                case "]":
                case ",":
                    throw new Exception(string.Format(@"unexpected token: ""{0}""", current));
                default:
                    var currentType = new ObjectType(current);
                    tokens.MoveNext();
                    if (tokens.Current != "[")
                    {
                        return currentType;
                    }
                    else
                    {
                        tokens.MoveNext();
                        CheckToken("]", tokens);
                        return new ArrayType(currentType);
                    }
            }
        }

        void CheckToken(string expectedToken, IEnumerator<string> tokens)
        {
            if (tokens.Current != expectedToken)
                throw new Exception(string.Format(@"expect ""{0}""", expectedToken));
            tokens.MoveNext();
        }

        public List<string> Parse(string type)
        {
            var result = new List<string>();

            using (StringReader reader = new StringReader(type))
            {
                StringBuilder builder = new StringBuilder();

                while (true)
                {
                    var peek = reader.Read();

                    switch (peek)
                    {
                        case -1:
                        case ' ':
                            if (builder.Length > 0)
                            {
                                result.Add(builder.ToString());
                                builder.Length = 0;
                            }
                            break;
                        case '<':
                        case '>':
                        case '[':
                        case ']':
                        case ',':
                            if (builder.Length > 0)
                            {
                                result.Add(builder.ToString());
                                builder.Length = 0;
                            }
                            result.Add(new string((char)peek, 1));
                            break;
                        default:
                            builder.Append((char)peek);
                            break;
                    }

                    if (peek == -1)
                        break;
                }
            }

            return result;
        }
    }

    public interface IType
    {
        string ToTypeName();
        string ToDeserializeAction();
        string ToSerializeArguments(int depth);
    }

    public static class TypeExtension
    {
        public static string ToSerializeArguments(this IType type)
        {
            return type.ToSerializeArguments(0);
        }
    }

    public class DictionaryType : IType
    {
        IType keyType;
        IType valueType;

        public DictionaryType(IType keyType, IType valueType)
        {
            this.keyType = keyType;
            this.valueType = valueType;
        }

        public string ToTypeName() { return string.Format("Dictionary<{0}, {1}>", keyType.ToTypeName(), valueType.ToTypeName()); }
        public string ToDeserializeAction() { return string.Format("d.GetDictionary(() => {0}, () => {1})", keyType.ToDeserializeAction(), valueType.ToDeserializeAction()); }
        public string ToSerializeArguments(int depth) { return string.Format(", (b{0}, t{0}) => b{0}.AppendJson(t{0}{1}), (b{0}, t{0}) => b{0}.AppendJson(t{0}{2})", depth == 0 ? "" : depth.ToString(), keyType.ToSerializeArguments(), valueType.ToSerializeArguments()); }
    }

    public class ListType : IType
    {
        IType type;

        public ListType(IType type) { this.type = type; }

        public string ToTypeName() { return string.Format("List<{0}>", type.ToTypeName()); }
        public string ToDeserializeAction() { return string.Format("d.GetList(() => {0})", type.ToDeserializeAction()); }
        public string ToSerializeArguments(int depth) { return string.Format(", (b{0}, t{0}) => b{0}.AppendJson(t{0}{1})", depth == 0 ? "" : depth.ToString(), type.ToSerializeArguments()); }
    }

    public class ArrayType : IType
    {
        IType type;

        public ArrayType(IType type) { this.type = type; }

        public string ToTypeName() { return string.Format("{0}[]", type.ToTypeName()); }
        public string ToDeserializeAction() { return string.Format("d.GetArray(() => {0})", type.ToDeserializeAction()); }
        public string ToSerializeArguments(int depth) { return string.Format(", (b{0}, t{0}) => b{0}.AppendJson(t{0}{1})", depth == 0 ? "" : depth.ToString(), type.ToSerializeArguments()); }
    }

    public class ObjectType : IType
    {
        string typeName;

        public ObjectType(string typeName) { this.typeName = typeName; }

        public string ToTypeName() { return typeName; }
        public virtual string ToDeserializeAction() { return string.Format("d.GetObject<{0}>()", this.ToTypeName()); }
        public string ToSerializeArguments(int depth) { return ""; }
    }

    public class StringType : ObjectType
    {
        public StringType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetString()"; }
    }

    public class NullableBoolType : ObjectType
    {
        public NullableBoolType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetNullableBool()"; }
    }

    public class BoolType : ObjectType
    {
        public BoolType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetBool()"; }
    }

    public class NullableUnsignedLongType : ObjectType
    {
        public NullableUnsignedLongType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetNullableUnsignedLong()"; }
    }

    public class UnsignedLongType : ObjectType
    {
        public UnsignedLongType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetUnsignedLong()"; }
    }

    public class NullableLongType : ObjectType
    {
        public NullableLongType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetNullableLong()"; }
    }

    public class LongType : ObjectType
    {
        public LongType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetLong()"; }
    }

    public class NullableUnsignedIntType : ObjectType
    {
        public NullableUnsignedIntType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetNullableUnsignedInt()"; }
    }

    public class UnsignedIntType : ObjectType
    {
        public UnsignedIntType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetUnsignedInt()"; }
    }

    public class NullableIntType : ObjectType
    {
        public NullableIntType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetNullableInt()"; }
    }

    public class IntType : ObjectType
    {
        public IntType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetInt()"; }
    }

    public class NullableCharType : ObjectType
    {
        public NullableCharType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetNullableChar()"; }
    }

    public class CharType : ObjectType
    {
        public CharType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetChar()"; }
    }

    public class NullableUnsignedShortType : ObjectType
    {
        public NullableUnsignedShortType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetNullableUnsignedShort()"; }
    }

    public class UnsignedShortType : ObjectType
    {
        public UnsignedShortType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetUnsignedShort()"; }
    }

    public class NullableShortType : ObjectType
    {
        public NullableShortType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetNullableShort()"; }
    }

    public class ShortType : ObjectType
    {
        public ShortType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetShort()"; }
    }

    public class NullableByteType : ObjectType
    {
        public NullableByteType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetNullableByte()"; }
    }

    public class ByteType : ObjectType
    {
        public ByteType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetByte()"; }
    }

    public class NullableSignedByteType : ObjectType
    {
        public NullableSignedByteType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetNullableSignedByte()"; }
    }

    public class SignedByteType : ObjectType
    {
        public SignedByteType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetSignedByte()"; }
    }

    public class NullableDoubleType : ObjectType
    {
        public NullableDoubleType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetNullableDouble()"; }
    }

    public class DoubleType : ObjectType
    {
        public DoubleType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetDouble()"; }
    }

    public class NullableFloatType : ObjectType
    {
        public NullableFloatType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetNullableFloat()"; }
    }

    public class FloatType : ObjectType
    {
        public FloatType(string typeName) : base(typeName) { }
        public override string ToDeserializeAction() { return "d.GetFloat()"; }
    }
}
