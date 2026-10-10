using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata.Structs.Definitions;

public class Il2CppGenericContainerDefinition : MetadataBase {
    private readonly int _index;
    
    public List<Il2CppGenericParameterDefinition> Parameters { get; private set; }

    public Il2CppGenericContainerDefinition(int index) : base(MetadataContext.Instance.Metadata,
        MetadataHeader.Instance.GenericContainerOffset + MetadataConstants.GenericContainerDefinitionSize * index) {
        _index = index;
        Populate();
    }
    
    [MetadataTag(0x00, MetadataOperation.SUB, 214875017)]
    public int TypeArgc { get; private set; }
    
    [MetadataTag(0x04, MetadataOperation.SUB, 1976395631)]
    public int IsMethod { get; private set; }
    
    [MetadataTag(0x0C, MetadataOperation.SUB, 248843855)]
    public int GenericParameterStart { get; private set; }

    protected override void PostProcess() {
        ulong lcd = (748293795UL * ((1997422568UL * (((0x3D6913E0AF40UL * (ulong)_index) + 0xA64CAD60FA052C0UL) >> 23)) >> 11)) >> 23;

        TypeArgc ^= (int)lcd;
        IsMethod ^= (int)lcd;
        GenericParameterStart ^= (int)lcd;

        Parameters = new List<Il2CppGenericParameterDefinition>();
        for (int i = 0; i < TypeArgc; i++)
            Parameters.Add(new Il2CppGenericParameterDefinition(GenericParameterStart + i));
    }
}
