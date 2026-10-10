using System.Runtime.InteropServices;
using System.Text;
using HSRGlobalMetadata.Structs;
using HSRGlobalMetadata.Structs.Definitions;
using HSRGlobalMetadata.Structs.Runtime;
using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata.Output;

public static class DumpWriter {
    private static readonly string[] _pads = Enumerable.Range(0, 20).Select(i => new string(' ', i * 4)).ToArray();
    
    public static void Write(string inputDir) {
        WriteToDirectory(Path.Combine(inputDir, "dump"));
    }

    public static void WriteToDirectory(string outputDir) {
        Directory.CreateDirectory(outputDir);
        string outputPath = Path.Combine(outputDir, "dump.cs");
        
        var header = MetadataHeader.Instance;
        int imageCount = header.ImagesSize / MetadataConstants.ImageDefinitionSize;
        
        using var writer = new StreamWriter(outputPath, false, Encoding.UTF8, bufferSize: 1 << 20);

        WriteAssemblyInfo(writer);

        var imageResults = new string[imageCount];

        Parallel.For(0, imageCount, i => {
            var imageDef = MetadataCache.Images[i];
            var sb = new StringBuilder(imageDef.TypeCount * 512);
            
            for (int j = 0; j < imageDef.TypeCount; j++) {
                var typeDef = MetadataCache.TypeDefs[imageDef.TypeStart + j];
                if (typeDef.DeclaringTypeIndex != -1) continue;
                WriteType(sb, typeDef, imageDef, j + imageDef.TypeStart, header, 0);
            }

            imageResults[i] = sb.ToString();
        });

        foreach (var result in imageResults)
            writer.Write(result);
    }
    
    static void WriteType(StringBuilder sb, Il2CppTypeDefinition typeDef, Il2CppImageDefinition imageDef,
        int typeDefIndex, MetadataHeader header, int indent) {
        string pad = _pads[indent];
        string innerPad = _pads[indent + 1];
    
        if (indent == 0) {
            sb.AppendLine()
              .Append(pad).Append("// TypeDefIndex: ").AppendLine(typeDefIndex.ToString())
              .Append(pad).Append("// AssemblyIndex: ").Append(imageDef.AssemblyIndex)
              .Append(", TypeIndexInAssembly: ").AppendLine((typeDefIndex - imageDef.TypeStart).ToString())
              .Append(pad).Append("// Assembly: ").AppendLine(imageDef.Name)
              .Append(pad).Append("// Namespace: ").AppendLine(typeDef.Namespace);

            if (typeDef.Namespace.Length > 0)
                sb.Append(pad).Append("// Full Name: ").Append(typeDef.Namespace).Append('.').AppendLine(typeDef.Name);
            else
                sb.Append(pad).Append("// Full Name: ").AppendLine(typeDef.Name);
        }
    
        string typeAttrs = AttributeFormatter.FormatType((TypeAttributes)typeDef.Flags, typeDef.Bitfield, pad);
        
        string typeName;
        string constraintSuffix = "";
        if (typeDef.GenericContainerIndex != -1) {
            var container = new Il2CppGenericContainerDefinition(typeDef.GenericContainerIndex);
            var paramStrings = container.Parameters.Select(p => p.Name);
            int backtickIdx = typeDef.Name.IndexOf('`');
            string baseName = backtickIdx >= 0 ? typeDef.Name.Substring(0, backtickIdx) : typeDef.Name;
            typeName = $"{baseName}<{string.Join(", ", paramStrings)}>";
            var constraints = container.Parameters
                .Where(p => p.Constraints.Count > 0)
                .Select(p => $"where {p.Name} : {string.Join(", ", p.Constraints)}");
            constraintSuffix = constraints.Any() ? $" {string.Join(" ", constraints)}" : "";
        } else {
            typeName = typeDef.Name;
        }
        
        if (indent != 0) sb.AppendLine();
        sb.Append(pad).Append(typeAttrs).Append(' ').Append(typeName);
    
        var inherits = new List<string>();
        if (typeDef.ParentIndex != -1)
            inherits.Add(typeDef.Parent.Name());
        if (typeDef.InterfaceCount > 0) {
            var metadata = MetadataContext.Instance.Metadata;
            int indexSize = MetadataConstants.IndexSize;
            int baseOffset = header.InterfaceOffset + typeDef.InterfaceStart * indexSize;
            for (int k = 0; k < typeDef.InterfaceCount; k++) {
                int typeInterface = MemoryMarshal.Read<int>(metadata.AsSpan(baseOffset + k * indexSize));
                if (typeInterface != -1)
                    inherits.Add(Il2CppType.FromIndex(typeInterface).Name());
            }
        }
        if (inherits.Count > 0)
            sb.Append(" : ").Append(string.Join(", ", inherits));
    
        sb.Append(constraintSuffix).AppendLine(" {");

        if (typeDef.FieldCount > 0) {
            var fieldOffsetIndex = typeDef.GetFieldOffsetIndex();
            sb.AppendLine().Append(innerPad).AppendLine("// Fields:");
            for (int n = 0; n < typeDef.FieldCount; n++) {
                var fieldDef = new Il2CppFieldDefinition(typeDef.FieldStart, fieldOffsetIndex, typeDef.FieldStart + n);
                WriteField(sb, fieldDef, indent + 1);
            }
        }
        
        if (typeDef.PropertyCount > 0)
            WriteProperties(sb, typeDef, indent + 1);
        
        if (typeDef.EventCount > 0)
            WriteEvents(sb, typeDef, indent + 1);
        
        if (typeDef.MethodCount > 0)
            WriteMethods(sb, typeDef, indent + 1);
    
        if (typeDef.NestedTypeCount > 0) {
            var metadata = MetadataContext.Instance.Metadata;
            int indexSize = MetadataConstants.IndexSize;
            int baseOffset = header.NestedTypesOffset + typeDef.NestedTypeStart * indexSize;
            for (int n = 0; n < typeDef.NestedTypeCount; n++) {
                int nestedIndex = MemoryMarshal.Read<int>(metadata.AsSpan(baseOffset + n * indexSize));
                var nestedTypeDef = new Il2CppTypeDefinition(nestedIndex);
                WriteType(sb, nestedTypeDef, imageDef, nestedIndex, header, indent + 1);
            }
        }
    
        sb.Append(pad).AppendLine("}");
    }
    
    static void WriteEvents(StringBuilder sb, Il2CppTypeDefinition typeDef, int indent) {
        string pad = _pads[indent];
        sb.AppendLine().Append(pad).AppendLine("// Events:");

        for (int n = 0; n < typeDef.EventCount; n++) {
            var ev = new Il2CppEventDefinition(typeDef.EventStart + n);
        
            string eventAttrs = "";
            if (ev.AddMethodIndex != -1) {
                var addMethod = new Il2CppMethodDefinition(typeDef.MethodStart + ev.AddMethodIndex);
                eventAttrs = AttributeFormatter.FormatMethod((MethodAttributes)addMethod.Flags);
            } else if (ev.RemoveMethodIndex != -1) {
                var removeMethod = new Il2CppMethodDefinition(typeDef.MethodStart + ev.RemoveMethodIndex);
                eventAttrs = AttributeFormatter.FormatMethod((MethodAttributes)removeMethod.Flags);
            }

            string typeName = "object";
            if (ev.AddMethodIndex != -1) {
                var addMethod = new Il2CppMethodDefinition(typeDef.MethodStart + ev.AddMethodIndex);
                if (addMethod.ParametersCount > 0) {
                    var param = new Il2CppParameterDefinition(addMethod.ParametersStart);
                    typeName = param.Type.Name();
                }
            }

            string add = ev.AddMethodIndex != -1 ? "add; " : "";
            string remove = ev.RemoveMethodIndex != -1 ? "remove; " : "";
            string raise = ev.RaiseMethodIndex != -1 ? "raise; " : "";
            sb.Append(pad).Append(eventAttrs).Append(" event ").Append(typeName)
              .Append(' ').Append(ev.Name).Append(" { ").Append(add).Append(remove).Append(raise).AppendLine("}");
        }
    }

    static void WriteProperties(StringBuilder sb, Il2CppTypeDefinition typeDef, int indent) {
        string pad = _pads[indent];
        sb.AppendLine().Append(pad).AppendLine("// Properties:");

        for (int n = 0; n < typeDef.PropertyCount; n++) {
            var prop = new Il2CppPropertyDefinition(typeDef.PropertyStart + n);

            string typeName = "object";
            if (prop.GetMethodIndex != -1) {
                var getter = new Il2CppMethodDefinition(typeDef.MethodStart + prop.GetMethodIndex);
                typeName = getter.ReturnType.Name();
            } else if (prop.SetMethodIndex != -1) {
                var setter = new Il2CppMethodDefinition(typeDef.MethodStart + prop.SetMethodIndex);
                if (setter.ParametersCount > 0) {
                    var param = new Il2CppParameterDefinition(setter.ParametersStart);
                    typeName = param.Type.Name();
                }
            }

            string getStr = prop.GetMethodIndex != -1 ? "get; " : "";
            string setStr = prop.SetMethodIndex != -1 ? "set; " : "";
            sb.Append(pad).Append(typeName).Append(' ').Append(prop.Name)
              .Append(" { ").Append(getStr).Append(setStr)
              .AppendLine("}");
        }
    }

    static void WriteField(StringBuilder sb, Il2CppFieldDefinition fieldDef, int indent) {
        string pad = _pads[indent];
        string attrs = AttributeFormatter.FormatField((FieldAttributes)fieldDef.Type.Attrs);
        string typeName = fieldDef.Type.Name();

        sb.Append(pad).Append(attrs).Append(' ').Append(typeName).Append(' ').Append(fieldDef.Name);
    
        if (((FieldAttributes)fieldDef.Type.Attrs & FieldAttributes.Literal) != 0 &&
            fieldDef.TryGetFieldStaticValue(out var staticValue)) {
            sb.Append(" = ");

            if (staticValue is string str)
                sb.Append('"').Append(str.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
            else if (staticValue is char ch)
                sb.Append('\'').Append(ch switch {
                    '\\' => "\\\\",
                    '\'' => "\\'",
                    '\0' => "\\0",
                    '\a' => "\\a",
                    '\b' => "\\b",
                    '\f' => "\\f",
                    '\n' => "\\n",
                    '\r' => "\\r",
                    '\t' => "\\t",
                    '\v' => "\\v",
                    _ => ch.ToString()
                }).Append('\'');
            else
                sb.Append(staticValue);
        }

        sb.Append(" // Offset: 0x").AppendLine(fieldDef.Offset.ToString("X"));
    }
        
    static void WriteMethods(StringBuilder sb, Il2CppTypeDefinition typeDef, int indent) {
        string pad = _pads[indent];
        bool isInterface = ((TypeAttributes)typeDef.Flags & TypeAttributes.ClassSemanticMask) == TypeAttributes.Interface;
        
        var constructors = new List<Il2CppMethodDefinition>();
        var methods = new List<Il2CppMethodDefinition>();
        
        for (int n = 0; n < typeDef.MethodCount; n++) {
            var method = new Il2CppMethodDefinition(typeDef.MethodStart + n);
            if (method.Name is ".ctor" or ".cctor")
                constructors.Add(method);
            else
                methods.Add(method);
        }
    
        if (constructors.Count > 0) {
            sb.AppendLine().Append(pad).AppendLine("// Constructors:");
            foreach (var ctor in constructors)
                WriteMethod(sb, ctor, pad, isInterface);
        }
    
        if (methods.Count > 0) {
            sb.AppendLine().Append(pad).AppendLine("// Methods:");
            foreach (var method in methods)
                WriteMethod(sb, method, pad, isInterface);
        }
    }
    
    static void WriteMethod(StringBuilder sb, Il2CppMethodDefinition method, string pad, bool isInterface) {
        string methodName;
        string genericSuffix = "";
        string constraintSuffix = "";
    
        if (method.GenericContainerIndex != -1) {
            var container = new Il2CppGenericContainerDefinition(method.GenericContainerIndex);
            var paramStrings = container.Parameters.Select(p => p.Name);
            int backtickIdx = method.Name.IndexOf('`');
            methodName = backtickIdx >= 0 ? method.Name.Substring(0, backtickIdx) : method.Name;
            genericSuffix = $"<{string.Join(", ", paramStrings)}>";
            var constraints = container.Parameters
                .Where(p => p.Constraints.Count > 0)
                .Select(p => $"where {p.Name} : {string.Join(", ", p.Constraints)}");
            constraintSuffix = constraints.Any() ? $" {string.Join(" ", constraints)}" : "";
        } else {
            methodName = method.Name;
        }
    
        string paramString = "";
        if (method.ParametersCount > 0) {
            var paramParts = new List<string>(method.ParametersCount);
            for (int j = 0; j < method.ParametersCount; j++) {
                var param = new Il2CppParameterDefinition(method.ParametersStart + j);
                paramParts.Add(param.Name.Length != 0
                    ? $"{param.Type.Name()} {param.Name}"
                    : param.Type.Name());
            }
            paramString = string.Join(", ", paramParts);
        }

        string attrs = isInterface ? "" : AttributeFormatter.FormatMethod((MethodAttributes)method.Flags);

        long imageBase = checked((long)PEHelper.ImageBase);
        long rva = method.MethodPointer > imageBase
            ? method.MethodPointer - imageBase
            : method.MethodPointer;

        sb.Append(pad).Append(attrs).Append(' ')
          .Append(method.ReturnType.Name()).Append(' ')
          .Append(methodName).Append(genericSuffix)
          .Append('(').Append(paramString).Append(')')
          .Append(constraintSuffix)
          .Append(" {} // VA: 0x").Append(method.MethodPointer.ToString("X"))
          .Append(", RVA: 0x").AppendLine(rva.ToString("X"));
    }

    static void WriteAssemblyInfo(StreamWriter writer) {
        for (int i = 0; i < MetadataCache.Images.Length; i++) {
            var imageDef = MetadataCache.Images[i];
            writer.WriteLine(
                $"// Assembly {i + 1}: {imageDef.Name}, Type Count: {imageDef.TypeCount}, Type Start: {imageDef.TypeStart}");
        }
    }
}
