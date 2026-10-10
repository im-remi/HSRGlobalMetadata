using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Structs.Runtime;
using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata.Structs.Definitions;

public class Il2CppGenericFunctionDefinition : MetadataBase {
    public Il2CppGenericFunctionDefinition(int index) : base(MetadataContext.Instance.Metadata,
        MetadataHeader.Instance.GenericMethodFunctionsDefsOffset + index * MetadataConstants.GenericFunctionDefinitionSize){
        Populate();
    }

    [MetadataTag(0x00)]
    public int MethodSpecIndex { get; private set; }

    [MetadataTag(0x04)]
    public int MethodIndex { get; private set; }

    [MetadataTag(0x08)]
    public ushort AdjustorThunkIndex { get; private set; }

    [MetadataTag(0x0A)]
    public ushort InvokerIndex { get; private set; }
}
