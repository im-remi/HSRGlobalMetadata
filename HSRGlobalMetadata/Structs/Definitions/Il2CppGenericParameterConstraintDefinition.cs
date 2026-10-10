using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Structs.Runtime;
using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata.Structs.Definitions;

public class Il2CppGenericParameterConstraintDefinition : MetadataBase {
    public string TypeName { get; private set; }

    public Il2CppGenericParameterConstraintDefinition(int index) : base(MetadataContext.Instance.Metadata,
        MetadataHeader.Instance.GenericParameterConstraintsOffset + MetadataConstants.GenericParameterConstraintDefinitionSize * index) {
        Populate();
    }

    [MetadataTag(0x00)] 
    public int ConstraintIndex { get; private set; }

    protected override void PostProcess() {
        TypeName = ConstraintIndex == -1 ? "" : Il2CppType.FromIndex(ConstraintIndex).Name();
    }
}
