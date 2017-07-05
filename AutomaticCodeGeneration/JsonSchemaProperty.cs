using System;
using System.Collections.Generic;
using System.Text;

namespace AcgJson.AutomaticCodeGeneration
{
    public class JsonSchemaProperty :
        // # inheritance begin
        // # inheritance end
        ISerializable, IDeserializable, IJsonSchemePropertySetter
    {
        public string Type { get; private set; }
        public string Description { get; private set; }

        // # function begin
        public void SaveNames(IType type, string cSharpProperty, string cSharpArgument, string jsonField)
        {
            this.TypeStructure = type;
            this.CSharpPropertyName = cSharpProperty;
            this.CSharpArgumentName = cSharpArgument;
            this.JsonFieldName = jsonField;
        }

        public IType TypeStructure { get; private set; }
        public string CSharpPropertyName { get; private set; }
        public string CSharpArgumentName { get; private set; }
        public string JsonFieldName { get; private set; }
        // # function end

        string IJsonSchemePropertySetter.Type { set { this.Type = value; } }
        string IJsonSchemePropertySetter.Description { set { this.Description = value; } }

        static readonly Dictionary<string, Action<IJsonSchemePropertySetter, Deserializer>> DeserializeActions = new Dictionary<string, Action<IJsonSchemePropertySetter, Deserializer>>()
        {
            { "Type" , (s, d) => { s.Type = d.GetString(); } },
            { "Description" , (s, d) => { s.Description = d.GetString(); } },
        };

        void IDeserializable.Deserialize(Deserializer deserializer)
        {
            deserializer.DeserializeObject(this, DeserializeActions);
        }

        public override string ToString()
        {
            return string.Format("[{0}]{1}", this.GetType().Name, ToJson());
        }

        public string ToJson()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendJson(this);
            return stringBuilder.ToString();
        }

        void ISerializable.AppendJson(StringBuilder stringBuilder)
        {
            stringBuilder.Append('{');

            stringBuilder.AppendJson("Type");
            stringBuilder.Append(':');
            stringBuilder.AppendJson(Type);

            stringBuilder.Append(',');

            stringBuilder.AppendJson("Description");
            stringBuilder.Append(':');
            stringBuilder.AppendJson(Description);

            stringBuilder.Append('}');
        }
    }

    public interface IJsonSchemePropertySetter
    {
        string Type { set; }
        string Description { set; }
    }

    // # class begin
    // # class end
}