
using HSRGlobalMetadata;
using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Structs;
using HSRGlobalMetadata.Structs.Runtime;
using HSRGlobalMetadata.Utils;

public class Il2CppCustomAttributeTypesDefinition : MetadataBase {
    [MetadataTag(0x00)]
    public int TypeIndex { get; private set; }

    public Il2CppType Type;

    public Il2CppCustomAttributeTypesDefinition(int index) : base(MetadataContext.Instance.Metadata, MetadataHeader.Instance.CustomAttributeTypesOffset + index * MetadataConstants.IndexSize) {
        Populate();
    }

    protected override void PostProcess() {
        if (TypeIndex != -1)
            Type = Il2CppType.FromIndex(TypeIndex);
    }
}