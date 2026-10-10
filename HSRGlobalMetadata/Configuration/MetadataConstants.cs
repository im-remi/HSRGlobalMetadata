using HSRGlobalMetadata.Structs.Runtime;

namespace HSRGlobalMetadata.Configuration;

internal static class MetadataConstants {
    public const int HeaderSize = 0x208;
    public const int MetadataMagic = 0x0059484D;

    public const int TypeDefinitionSize = 70;
    public const int ImageDefinitionSize = 40;
    public const int MethodDefinitionSize = 26;
    public const int FieldDefinitionSize = 8;
    public const int PropertyDefinitionSize = 10;
    public const int EventDefinitionSize = 14;
    public const int ParameterDefinitionSize = 8;

    public const int GenericContainerDefinitionSize = 16;
    public const int GenericParameterDefinitionSize = 14;
    public const int GenericFunctionDefinitionSize = 12;
    public const int GenericParameterConstraintDefinitionSize = 4;
    public const int GenericClassDefinitionSize = 8;
    public const int GenericInstDefinitionSize = 16;
    public const int ArrayDefinitionSize = 32;

    public static int Il2CppTypeDefinitionSize = Il2CppType.DetectTypeSize();

    public const int IndexSize = 4;
    public const int PointerSize = 8;

    public const byte TypeModifiersMask = 0x3F;
    public const byte TypeByReferenceMask = 0x40;
    public const byte TypePinnedMask = 0x80;
}