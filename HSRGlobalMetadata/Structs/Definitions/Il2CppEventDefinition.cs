using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata.Structs.Definitions;

public class Il2CppEventDefinition : MetadataBase {
    private readonly int _index;

    public string Name { get; private set; }

    [MetadataTag(0x00, MetadataOperation.XOR, 0x78981CB)]
    public int NameIndex { get; private set; }
    
    [MetadataTag(0x04, MetadataOperation.XOR, 0x53243DF3)]
    public int TypeIndex { get; private set; }
    
    [MetadataTag(0x0A, MetadataOperation.XOR, 0xE8CB)]
    public short AddMethodIndex { get; set; }

    [MetadataTag(0x08, MetadataOperation.XOR, 0x8450)]
    public short RaiseMethodIndex { get; set; }

    [MetadataTag(0x0C, MetadataOperation.XOR, 0x3CE2)]
    public short RemoveMethodIndex { get; set; }

    public Il2CppEventDefinition(int index) : base(MetadataContext.Instance.Metadata,
        MetadataHeader.Instance.EventsOffset + MetadataConstants.EventDefinitionSize * index) {
        _index = index;
        Populate();
    }

    protected override void PostProcess() {
        ulong lcd = (1598447864UL * ((1057473644UL * ((15580UL * (ulong)_index) ^ 0x550D63BAUL)) >> 19) + 0x26824684CA69CA48UL) >> 15;

        NameIndex = (int)(NameIndex - (uint)lcd);
        Name = StringProcessor.Decrypt(NameIndex);

        TypeIndex = (int)(TypeIndex- (uint)lcd);

        AddMethodIndex = (short)(AddMethodIndex - (ushort)lcd);
        RaiseMethodIndex = (short)(RaiseMethodIndex - (ushort)lcd);
        RemoveMethodIndex = (short)(RemoveMethodIndex - (ushort)lcd);
    }
}
