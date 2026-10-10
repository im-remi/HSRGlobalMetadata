using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Structs.Runtime;
using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata.Structs.Definitions;

public class Il2CppParameterDefinition : MetadataBase {
    private readonly int _index;

    [MetadataTag(0x00, MetadataOperation.XOR, 0x67E90DC5)]
    public int TypeIndex { get; private set; } 
    
    [MetadataTag(0x04, MetadataOperation.XOR, 0x7103092E)]
    public int NameIndex { get; private set; }
    
    public Il2CppType Type { get; private set; }
    public string Name { get; private set; }
    
    public Il2CppParameterDefinition(int index) : base(MetadataContext.Instance.Metadata,
        MetadataHeader.Instance.ParametersOffset + index * MetadataConstants.ParameterDefinitionSize) {
        _index = index;
        Populate();
    }

    protected override void PostProcess() {
        int lcd = (int)((ulong)(1488482466L * (long)((0x72E1D74B12BUL * (ulong)_index + 0x1911D05AFF5UL) >> 11)) - 2083554492);

        TypeIndex -= lcd;
        NameIndex -= lcd;

        Name = StringProcessor.Decrypt(NameIndex);
        if (TypeIndex != -1)
            Type = Il2CppType.FromIndex(TypeIndex);
    }
}
