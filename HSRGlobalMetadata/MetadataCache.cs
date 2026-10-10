using System.Runtime.InteropServices;
using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Structs;
using HSRGlobalMetadata.Structs.Definitions;
using HSRGlobalMetadata.Structs.Runtime;

namespace HSRGlobalMetadata;

public static class MetadataCache {
    public static Il2CppTypeDefinition[] TypeDefs { get; private set; }
    public static Il2CppImageDefinition[] Images { get; private set; }
    public static Il2CppType[] Types { get; private set; }
    public static Dictionary<int, (int typeIndex, int dataOffset)> FieldDefaultValues { get; private set; }
    public static Dictionary<int, Il2CppGenericFunctionDefinition> GenericFuncDefs { get; private set; } 
    public static Dictionary<uint, Il2CppCustomAttributeRangeDefinition> CustomAttributeRanges { get; private set; }
    public static Il2CppCustomAttributeTypesDefinition[] CustomAttributeTypes { get; private set; }
    
    public static void Initialize() {
        var header = MetadataHeader.Instance;
        int typeCount = header.TypeDefinitionsSize / MetadataConstants.TypeDefinitionSize;
        int imageCount = header.ImagesSize / MetadataConstants.ImageDefinitionSize;
        if (header.TypeDefinitionsSize > MetadataContext.Instance.Metadata.Length) {
          throw new Exception("Sanity check failed for metadata validity.");
        }        
        TypeDefs = new Il2CppTypeDefinition[typeCount];
        for (int i = 0; i < typeCount; i++)
            TypeDefs[i] = new Il2CppTypeDefinition(i);
            
        Images = new Il2CppImageDefinition[imageCount];
        for (int i = 0; i < imageCount; i++)
            Images[i] = new Il2CppImageDefinition(i);
        
        Types = new Il2CppType[MetadataRegistration.Instance.TypeInfoCount];
        for (int i = 0; i < MetadataRegistration.Instance.TypeInfoCount; i++) 
            Types[i] = Il2CppType.FromIndex(i);
        
        var metadata = MetadataContext.Instance.Metadata;
        int offset = header.FieldDefaultValuesOffset;
        int j = 0;
        var span = metadata.AsSpan(offset);
        int fieldDefaultValueSize = MetadataConstants.FieldDefaultValueDefinitionSize;
        FieldDefaultValues = new();
        while (offset + fieldDefaultValueSize * j + fieldDefaultValueSize <= metadata.Length) {
            int typeIndex = MemoryMarshal.Read<int>(span.Slice(fieldDefaultValueSize * j));
            int dataOffset = MemoryMarshal.Read<int>(span.Slice(fieldDefaultValueSize * j + 4));
            int fieldIndex = MemoryMarshal.Read<int>(span.Slice(fieldDefaultValueSize * j + 8));
            if (fieldIndex < 0 || fieldIndex > 10000000) break;
            if (typeIndex < 0 || typeIndex > MetadataRegistration.Instance.TypeInfoCount) break;
            FieldDefaultValues[fieldIndex] = (typeIndex, dataOffset);
            j++;
        }

        int k = 0;
        CustomAttributeRanges = new();
        var previousStart = -1;
        while (header.CustomAttributeRangeOffset + MetadataConstants.CustomAttributeRangeSize * k + MetadataConstants.CustomAttributeRangeSize <= metadata.Length) {
            var customAttributeRange = new Il2CppCustomAttributeRangeDefinition(k);
            if (customAttributeRange.Count < 0 || customAttributeRange.Count > 255 || customAttributeRange.Start < previousStart)
                break;

            if (!CustomAttributeRanges.TryAdd(customAttributeRange.Token, customAttributeRange) && (customAttributeRange.Token & 0xFF000000) != 0x20000000) {
                var existing = CustomAttributeRanges[customAttributeRange.Token];
                
                Console.WriteLine(
                    $"Duplicate custom attribute range for token 0x{customAttributeRange.Token:X8}: " +
                    $"new (Count: {customAttributeRange.Count}, Start: {customAttributeRange.Start}), " +
                    $"existing (Count: {existing.Count}, Start: {existing.Start})");
            };
            previousStart = customAttributeRange.Start;
            k++;
        }

        int customAttributeTypesCount = previousStart + CustomAttributeRanges.Last().Value.Count;
        CustomAttributeTypes = new Il2CppCustomAttributeTypesDefinition[customAttributeTypesCount];
        for (int i = 0; i < customAttributeTypesCount; i++) 
            CustomAttributeTypes[i] = new Il2CppCustomAttributeTypesDefinition(i);

        int genericFuncCount = header.MethodSpecsSize / MetadataConstants.GenericFunctionDefinitionSize;
        GenericFuncDefs = new Dictionary<int, Il2CppGenericFunctionDefinition>(genericFuncCount);
        for (int i = 0; i < genericFuncCount; i++) {
            var genericFuncDef = new Il2CppGenericFunctionDefinition(i);
            GenericFuncDefs[genericFuncDef.MethodSpecIndex] = genericFuncDef;
        }
    }
}
