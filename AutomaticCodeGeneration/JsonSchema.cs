using System;
using System.Collections.Generic;
using System.Text;

namespace AcgJson.AutomaticCodeGeneration
{
    public class JsonSchema :
        // # inheritance begin
        // # inheritance end
        ISerializable, IDeserializable, IJsonSchemeSetter
    {
        public JsonSchema()
        {
        }

        public JsonSchema(
            string name,
            Dictionary<string, JsonSchemaProperty> properties)
        {
            this.Name = name;
            this.Properties = properties;
        }

        public string Name { get; private set; }
        public Dictionary<string, JsonSchemaProperty> Properties { get; private set; }

        // # function begin
        public bool IsConstructible { get { return true; } }
        public bool IsSerializable { get { return true; } }
        public bool IsDeserializable { get { return true; } }
        // # function end

        string IJsonSchemeSetter.Name { set { this.Name = value; } }
        Dictionary<string, JsonSchemaProperty> IJsonSchemeSetter.Properties { set { this.Properties = value; } }

        static readonly Dictionary<string, Action<IJsonSchemeSetter, Deserializer>> DeserializeActions = new Dictionary<string, Action<IJsonSchemeSetter, Deserializer>>()
        {
            { "Name" , (s, d) => { s.Name = d.GetString(); } },
            { "Properties", (s, d) => { s.Properties = d.GetDictionary(() => d.GetString(), () => d.GetObject<JsonSchemaProperty>()); } },
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

            stringBuilder.AppendJson("Name");
            stringBuilder.Append(':');
            stringBuilder.AppendJson(Name);

            stringBuilder.Append(',');

            stringBuilder.AppendJson("Properties");
            stringBuilder.Append(':');
            stringBuilder.AppendJson(Properties, (sb, t) => sb.AppendJson(t), (sb, t) => sb.AppendJson(t));

            stringBuilder.Append('}');
        }
    }

    public interface IJsonSchemeSetter
    {
        string Name { set; }
        Dictionary<string, JsonSchemaProperty> Properties { set; }
    }

    // # class begin
    // # class end
}