using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace AcgJson.AutomaticCodeGeneration
{
    public class Generator
    {
        public static void GenerateAll(GeneratorSetting setting)
        {
            if (!Directory.Exists(setting.SchemaDirUri.AbsolutePath))
                throw new Exception(setting.SchemaDirUri.AbsolutePath + " does not exist.");

            if (!Directory.Exists(setting.OutputDirUri.AbsolutePath))
                Directory.CreateDirectory(setting.OutputDirUri.AbsolutePath);

            Dictionary<string, Exception> exceptions = new Dictionary<string, Exception>();

            foreach (var path in Directory.GetFiles(setting.SchemaDirUri.AbsolutePath, Filter, SearchOption.AllDirectories))
            {
                var g = new Generator(path, setting);
                g.Generate();
                //try
                //{
                //    Generate(path, setting);
                //}
                //catch (Exception e)
                //{
                //    exceptions.Add(path, e);
                //}
            }

            if (exceptions.Count > 0)
            {
                if (setting.NotifyExceptionsFunc != null)
                    setting.NotifyExceptionsFunc.Invoke(exceptions);
                else
                    throw new Exception(exceptions
                        .Select(p => string.Format("[{0}] {1}", p.Key, p.Value.ToString()))
                        .Aggregate((s1, s2) => string.Format("{0}\n{1}", s1, s2)));
            }
        }

        public Generator(string schemaFilePath, GeneratorSetting setting)
        {
            this.schemaFilePath = schemaFilePath;
            this.setting = setting;

            convertActions = new Dictionary<string, Action<string>>()
            {
                { NameSpaceLabel, OutputNameSpace },
                { ClassNameLabel, OutputClassName },

                { "@InheritanceBlock", OutputInheritanceBlock },
                { "@FunctionBlock", OutputFunctionBlock },
                { "@ClassBlock", OutputClassBlock },

                { "@Interfaces", OutputInterfaces },
                { "@Constructor", OutputConstructor },
                { "@Peroperties", OutputProperties },
                { "@InterfacePeroperties", OutputInterfaceProperties },
                { "@DeserializeActions", OutputDeserializeActions },
                { "@Deserialize", OutputDeserialize },
                { "@ToString", OutputToString },
                { "@ToJson", OutputToJson },
                { "@AppendJson", OutputAppendJson },
                { "@SetterInterface", OutputSetterInterface },
            };
        }

        const string Filter = "*.json";
        const string Extension = ".cs";

        string schemaFilePath;
        GeneratorSetting setting;
        Dictionary<string, Action<string>> convertActions;

        JsonSchema jsonSchema;
        Dictionary<string, List<string>> savedScripts;
        StreamWriter csWriter;

        const string SerializableInterface = "ISerializable";
        const string DeserializableInterface = "IDeserializable";
        const string SetterInterfaceFromat = "I{0}Setter";

        const string NameSpaceLabel = "@NameSpace";
        const string ClassNameLabel = "@ClassName";

        public void Generate()
        {
            var schemaFileUri = new Uri(schemaFilePath);
            var relativeUri = setting.SchemaDirUri.MakeRelativeUri(schemaFileUri);
            var outputTempUri = new Uri(setting.OutputDirUri, relativeUri);

            var outputFileDir = Path.GetDirectoryName(outputTempUri.AbsolutePath);
            if (!Directory.Exists(outputFileDir))
                Directory.CreateDirectory(outputFileDir);

            using (var streamReader = new StreamReader(schemaFileUri.AbsolutePath))
            {
                var deserializer = new Deserializer(streamReader.ReadToEnd());
                jsonSchema = deserializer.GetObject<JsonSchema>();

                var typeAnalyzer = new TypeAnalyzer();

                foreach (var pair in jsonSchema.Properties)
                    pair.Value.SaveNames(
                        typeAnalyzer.Analyze(setting.ModifyTypeNameFunc.Invoke(pair.Value.Type)),
                        setting.ToCSharpPropertyNameFunc.Invoke(pair.Key),
                        setting.ToCSharpArgumentNameFunc.Invoke(pair.Key),
                        setting.ToJsonFieldNameFunc(pair.Key));
            }

            var outputFileUri = new Uri(outputTempUri, jsonSchema.Name + Extension);

            savedScripts = SaveScriptsFromOldFile(outputFileUri);
            OutputNewFile(outputFileUri);
        }

        Dictionary<string, List<string>> SaveScriptsFromOldFile(Uri outputFileUri)
        {
            var result = new Dictionary<string, List<string>>();

            if (!File.Exists(outputFileUri.AbsolutePath))
                return result;

            Dictionary<string, string> blockEdgePairs = new Dictionary<string, string>() {
                { setting.InheritanceBlockBegin, setting.InheritanceBlockEnd },
                { setting.FunctionBlockBegin, setting.FunctionBlockEnd },
                { setting.ClassBlockBegin, setting.ClassBlockEnd },
            };

            using (StreamReader streamReader = new StreamReader(outputFileUri.AbsolutePath))
            {
                while (!streamReader.EndOfStream)
                {
                    var line = streamReader.ReadLine();
                    var blockBegin = line.Trim();
                    string blockEnd;

                    if (blockEdgePairs.TryGetValue(blockBegin, out blockEnd))
                    {
                        var blockLines = new List<string>();
                        string blockLine = streamReader.ReadLine();

                        while (blockLine.Trim() != blockEnd)
                        {
                            blockLines.Add(blockLine);
                            blockLine = streamReader.ReadLine();
                        }

                        if (result.ContainsKey(blockBegin))
                            throw new Exception("same block has already existed: " + blockBegin);

                        result.Add(blockBegin, blockLines);
                    }
                }
            }

            return result;
        }

        void OutputNewFile(Uri outputFileUri)
        {
            using (csWriter = new StreamWriter(outputFileUri.AbsolutePath))
            {
                using (StreamReader templateReader = new StreamReader(setting.TemplateFileUri.AbsolutePath))
                {
                    while (!templateReader.EndOfStream)
                    {
                        var line = templateReader.ReadLine();
                        bool isConverted = false;

                        foreach (var pair in convertActions)
                        {
                            if (line.Contains(pair.Key))
                            {
                                pair.Value.Invoke(line);
                                isConverted = true;
                                break;
                            }
                        }

                        if (!isConverted)
                            csWriter.WriteLine(line);
                    }
                }
            }
        }

        string GetIndent(string line)
        {
            var trimmedLine = line.Trim();
            var index = line.IndexOf(trimmedLine);
            return line.Substring(0, index);
        }

        string GetSetterInterfaceName()
        {
            return string.Format(SetterInterfaceFromat, jsonSchema.Name);
        }

        #region converters

        void OutputNameSpace(string line)
        {
            csWriter.WriteLine(line.Replace(NameSpaceLabel, setting.NameSpace));
        }

        void OutputClassName(string line)
        {
            csWriter.WriteLine(line.Replace(ClassNameLabel, jsonSchema.Name));
        }

        void OutputInheritanceBlock(string line)
        {
            OutputBlock(line, setting.InheritanceBlockBegin, setting.InheritanceBlockEnd);
        }

        void OutputFunctionBlock(string line)
        {
            OutputBlock(line, setting.FunctionBlockBegin, setting.FunctionBlockEnd);
            csWriter.WriteLine();
        }

        void OutputClassBlock(string line)
        {
            OutputBlock(line, setting.ClassBlockBegin, setting.ClassBlockEnd);
            csWriter.WriteLine();
        }

        void OutputBlock(string line, string blockBegin, string blockEnd)
        {
            var indent = GetIndent(line);

            csWriter.Write(indent);
            csWriter.WriteLine(blockBegin);

            List<string> savedLines;
            if (savedScripts.TryGetValue(blockBegin, out savedLines))
            {
                foreach (var l in savedLines)
                    csWriter.WriteLine(l);
            }

            csWriter.Write(indent);
            csWriter.WriteLine(blockEnd);
        }

        void OutputInterfaces(string line)
        {
            var indent = GetIndent(line);
            var interfaces = new List<string>();

            if (jsonSchema.IsSerializable)
                interfaces.Add(SerializableInterface);

            if (jsonSchema.IsDeserializable)
            {
                interfaces.Add(DeserializableInterface);
                interfaces.Add(GetSetterInterfaceName());
            }

            if (interfaces.Count > 0)
            {
                var writer = csWriter;
                csWriter.Write(indent);
                csWriter.WriteLine(interfaces.Aggregate((s1, s2) => string.Format("{0}, {1}", s1, s2)));
            }
        }

        void OutputConstructor(string line)
        {
            if (!jsonSchema.IsConstructible)
                return;

            var baseIndent = GetIndent(line);

            // zero argument constructor
            csWriter.Write(baseIndent);
            csWriter.WriteLine(string.Format("public {0}()", jsonSchema.Name));

            csWriter.Write(baseIndent);
            csWriter.WriteLine("{");

            csWriter.Write(baseIndent);
            csWriter.WriteLine("}");

            csWriter.WriteLine("");

            // all arguments constructor
            csWriter.Write(baseIndent);
            csWriter.WriteLine(string.Format("public {0}(", jsonSchema.Name));

            int index = 0;
            foreach (var property in jsonSchema.Properties.Values)
            {
                csWriter.Write(baseIndent);
                csWriter.Write(setting.Indent);
                csWriter.Write(property.TypeStructure.ToTypeName());
                csWriter.Write(" ");
                csWriter.Write(property.CSharpArgumentName);

                if (index < jsonSchema.Properties.Count - 1)
                    csWriter.Write(",");
                else
                    csWriter.Write(")");

                csWriter.WriteLine();
                index++;
            }

            csWriter.Write(baseIndent);
            csWriter.WriteLine("{");

            foreach (var property in jsonSchema.Properties.Values)
            {
                csWriter.Write(baseIndent);
                csWriter.Write(setting.Indent);
                csWriter.Write("this.");
                csWriter.Write(property.CSharpPropertyName);
                csWriter.Write(" = ");
                csWriter.Write(property.CSharpArgumentName);
                csWriter.Write(";");
                csWriter.WriteLine();
            }

            csWriter.Write(baseIndent);
            csWriter.WriteLine("}");

            csWriter.WriteLine();
        }

        void OutputProperties(string line)
        {
            var writer = csWriter;
            var schema = jsonSchema;
            var baseIndent = GetIndent(line);

            foreach (var property in jsonSchema.Properties.Values)
            {
                csWriter.Write(baseIndent);
                csWriter.Write("public ");
                csWriter.Write(property.TypeStructure.ToTypeName());
                csWriter.Write(" ");
                csWriter.Write(property.CSharpPropertyName);
                csWriter.Write(" { get; private set; }");
                csWriter.WriteLine();
            }

            csWriter.WriteLine();
        }

        void OutputInterfaceProperties(string line)
        {
            if (!jsonSchema.IsDeserializable)
                return;

            var baseIndent = GetIndent(line);

            foreach (var property in jsonSchema.Properties.Values)
            {
                csWriter.Write(baseIndent);
                csWriter.Write(property.TypeStructure.ToTypeName());
                csWriter.Write(" ");
                csWriter.Write(GetSetterInterfaceName());
                csWriter.Write(".");
                csWriter.Write(property.CSharpPropertyName);
                csWriter.Write(" { set { this.");
                csWriter.Write(property.CSharpPropertyName);
                csWriter.Write(" = value; } }");
                csWriter.WriteLine();
            }

            csWriter.WriteLine();
        }

        void OutputDeserializeActions(string line)
        {
            if (!jsonSchema.IsDeserializable)
                return;

            var writer = csWriter;
            var schema = jsonSchema;
            var baseIndent = GetIndent(line);

            csWriter.Write(baseIndent);
            csWriter.Write("readonly Dictionary<string, Action<");
            csWriter.Write(GetSetterInterfaceName());
            csWriter.Write(", Deserializer>> DeserializeActions = new Dictionary<string, Action<");
            csWriter.Write(GetSetterInterfaceName());
            csWriter.Write(", Deserializer>>()");
            csWriter.WriteLine();

            csWriter.Write(baseIndent);
            csWriter.WriteLine("{");

            foreach (var property in jsonSchema.Properties.Values)
            {
                csWriter.Write(baseIndent);
                csWriter.Write(setting.Indent);
                csWriter.Write("{ \"");
                csWriter.Write(property.JsonFieldName);
                csWriter.Write("\", (s, d) => { s.");
                csWriter.Write(property.CSharpPropertyName);
                csWriter.Write(" = ");
                csWriter.Write(property.TypeStructure.ToDeserializeAction());
                csWriter.Write(": } },");
                csWriter.WriteLine();
            }

            csWriter.Write(baseIndent);
            csWriter.WriteLine("}");

            csWriter.WriteLine();
        }

        void OutputDeserialize(string line)
        {
            if (!jsonSchema.IsDeserializable)
                return;

            var writer = csWriter;
            var baseIndent = GetIndent(line);

            csWriter.Write(baseIndent);
            csWriter.Write("void ");
            csWriter.Write(GetSetterInterfaceName());
            csWriter.Write(".Deserialize(Deserializer deserializer)");
            csWriter.WriteLine();

            csWriter.Write(baseIndent);
            csWriter.WriteLine("{");

            csWriter.Write(baseIndent);
            csWriter.Write(setting.Indent);
            csWriter.WriteLine("deserializer.DeserializeObject(this, DeserializeActions);");

            csWriter.Write(baseIndent);
            csWriter.WriteLine("}");

            csWriter.WriteLine();
        }

        void OutputToString(string line)
        {
            if (!jsonSchema.IsSerializable)
                return;

            var writer = csWriter;
            var baseIndent = GetIndent(line);

            csWriter.Write(baseIndent);
            csWriter.WriteLine("public override string ToString()");

            csWriter.Write(baseIndent);
            csWriter.WriteLine("{");

            csWriter.Write(baseIndent);
            csWriter.Write(setting.Indent);
            csWriter.WriteLine(@"return string.Format(""[{0}]{1}"", this.GetType().Name, ToJson());");

            csWriter.Write(baseIndent);
            csWriter.WriteLine("}");

            csWriter.WriteLine();
        }

        void OutputToJson(string line)
        {
            if (!jsonSchema.IsSerializable)
                return;

            var writer = csWriter;
            var baseIndent = GetIndent(line);

            csWriter.Write(baseIndent);
            csWriter.WriteLine("public string ToJson()");

            csWriter.Write(baseIndent);
            csWriter.WriteLine("{");

            csWriter.Write(baseIndent);
            csWriter.Write(setting.Indent);
            csWriter.WriteLine("StringBuilder stringBuilder = new StringBuilder();");

            csWriter.Write(baseIndent);
            csWriter.Write(setting.Indent);
            csWriter.WriteLine("stringBuilder.AppendJson(this);");

            csWriter.Write(baseIndent);
            csWriter.Write(setting.Indent);
            csWriter.WriteLine("return stringBuilder.ToString();");

            csWriter.Write(baseIndent);
            csWriter.WriteLine("}");

            csWriter.WriteLine();
        }

        void OutputAppendJson(string line)
        {
            if (!jsonSchema.IsSerializable)
                return;

            var writer = csWriter;
            var schema = jsonSchema;
            var baseIndent = GetIndent(line);

            csWriter.Write(baseIndent);
            csWriter.WriteLine("void ISerializable.AppendJson(StringBuilder stringBuilder)");

            csWriter.Write(baseIndent);
            csWriter.WriteLine("{");

            csWriter.Write(baseIndent);
            csWriter.Write(setting.Indent);
            csWriter.WriteLine("stringBuilder.Append('{');");

            csWriter.WriteLine();

            int index = 0;
            foreach (var property in jsonSchema.Properties.Values)
            {
                csWriter.Write(baseIndent);
                csWriter.Write(setting.Indent);
                csWriter.Write("stringBuilder.AppendJson(\"");
                csWriter.Write(property.JsonFieldName);
                csWriter.Write("\");");
                csWriter.WriteLine();

                csWriter.Write(baseIndent);
                csWriter.Write(setting.Indent);
                csWriter.WriteLine("stringBuilder.Append(':');");

                csWriter.Write(baseIndent);
                csWriter.Write(setting.Indent);
                csWriter.Write("stringBuilder.Append(");
                csWriter.Write(property.CSharpPropertyName);
                csWriter.Write(property.TypeStructure.ToSerializeArguments());
                csWriter.WriteLine(");");

                csWriter.WriteLine();

                if (index < jsonSchema.Properties.Count - 1)
                {
                    csWriter.Write(baseIndent);
                    csWriter.Write(setting.Indent);
                    csWriter.WriteLine("stringBuilder.Append(',');");

                    csWriter.WriteLine();
                }

                index++;
            }

            csWriter.Write(baseIndent);
            csWriter.Write(setting.Indent);
            csWriter.WriteLine("stringBuilder.Append('}');");

            csWriter.Write(baseIndent);
            csWriter.WriteLine("}");

            csWriter.WriteLine();
        }

        void OutputSetterInterface(string line)
        {
            if (!jsonSchema.IsDeserializable)
                return;

            var writer = csWriter;
            var schema = jsonSchema;
            var baseIndent = GetIndent(line);

            csWriter.Write(baseIndent);
            csWriter.Write("public interface ");
            csWriter.Write(GetSetterInterfaceName());
            csWriter.WriteLine();

            csWriter.Write(baseIndent);
            csWriter.WriteLine("{");

            foreach (var property in jsonSchema.Properties.Values)
            {
                csWriter.Write(baseIndent);
                csWriter.Write(setting.Indent);
                csWriter.Write(property.TypeStructure.ToTypeName());
                csWriter.Write(" ");
                csWriter.Write(property.CSharpPropertyName);
                csWriter.Write(" { set; }");
                csWriter.WriteLine();
            }

            csWriter.Write(baseIndent);
            csWriter.WriteLine("}");

            csWriter.WriteLine();
        }

        #endregion
    }

    public class GeneratorSetting
    {
        public Uri SchemaDirUri { get; private set; }
        public Uri OutputDirUri { get; private set; }
        public string NameSpace { get; private set; }

        public Func<string, string> ToJsonFieldNameFunc { get; set; }
        public Func<string, string> ToCSharpPropertyNameFunc { get; set; }
        public Func<string, string> ToCSharpArgumentNameFunc { get; set; }
        public Func<string, string> ModifyTypeNameFunc { get; set; }
        public Action<Dictionary<string, Exception>> NotifyExceptionsFunc { get; set; }

        public string InheritanceBlockBegin { get; set; }
        public string InheritanceBlockEnd { get; set; }
        public string FunctionBlockBegin { get; set; }
        public string FunctionBlockEnd { get; set; }
        public string ClassBlockBegin { get; set; }
        public string ClassBlockEnd { get; set; }

        public Uri TemplateFileUri { get; private set; }
        public string TemplateFilePath { set { TemplateFileUri = ConvertToAbsoluteUri(value, false); } }

        public string Indent { get; set; }

        public GeneratorSetting(
            string schemaDirPath,
            string outputDirPath,
            string nameSpace)
        {
            this.SchemaDirUri = ConvertToAbsoluteUri(schemaDirPath, true);
            this.OutputDirUri = ConvertToAbsoluteUri(outputDirPath, true);
            this.NameSpace = nameSpace;

            this.ToJsonFieldNameFunc = s => s;
            this.ToCSharpPropertyNameFunc = s => s;
            this.ToCSharpArgumentNameFunc = s =>
            {
                var propertyName = this.ToCSharpPropertyNameFunc.Invoke(s);
                return string.Format("{0}{1}",
                    char.ToLowerInvariant(propertyName[0]),
                    propertyName.Substring(1));
            };
            this.ModifyTypeNameFunc = s => s;
            this.NotifyExceptionsFunc = exceptions =>
            {
                throw new Exception(exceptions
                    .Select(p => string.Format("[{0}] {1}", p.Key, p.Value.ToString()))
                    .Aggregate((s1, s2) => string.Format("{0}\n{1}", s1, s2)));
            };

            this.InheritanceBlockBegin = "// # inheritance block begin";
            this.InheritanceBlockEnd = "// # inheritance block end";
            this.FunctionBlockBegin = "// # function block begin";
            this.FunctionBlockEnd = "// # function block end";
            this.ClassBlockBegin = "// # class block begin";
            this.ClassBlockEnd = "// # class block end";

            this.TemplateFilePath = "JsonClass.template";

            this.Indent = "    ";
        }

        Uri ConvertToAbsoluteUri(string path, bool isDir)
        {
            var uri = new Uri(path, UriKind.RelativeOrAbsolute);

            if (!uri.IsAbsoluteUri)
                uri = new Uri(Path.GetFullPath(path));

            if (isDir)
            {
                if (!uri.AbsoluteUri.EndsWith("/"))
                    uri = new Uri(uri.AbsoluteUri + "/");
            }
            else
            {
                if (uri.AbsoluteUri.EndsWith("/"))
                    uri = new Uri(uri.AbsoluteUri.Remove(uri.AbsoluteUri.Length - 1));
            }

            return uri;
        }
    }
}
