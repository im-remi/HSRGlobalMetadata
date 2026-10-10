using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata.Structs.Definitions;

public class Il2CppPropertyDefinition : MetadataBase {
    private readonly int _index;

    [MetadataTag(0x00, MetadataOperation.XOR, 0x6199063C)]
    public int NameIndex { get; set; }

    [MetadataTag(0x04, MetadataOperation.XOR, 0x4B8F)]
    public short SetMethodIndex { get; set; }

    [MetadataTag(0x06, MetadataOperation.XOR, 0xFA36)]
    public short GetMethodIndex { get; set; }

    public string Name { get; private set; }

    public Il2CppPropertyDefinition(int index) : base(MetadataContext.Instance.Metadata,
        MetadataHeader.Instance.PropertiesOffset + MetadataConstants.PropertyDefinitionSize * index) {
        _index = index;
        Populate();
    }

    protected override void PostProcess() {
        ulong lcd = (((1831379439UL * ((6035UL * (ulong)_index) ^ 0x280E7A20UL)) >> 17) ^ 0x727EFF5BUL) + 1664893910 ^ 0x4ADBD505;

        NameIndex -= (int)lcd;
        SetMethodIndex = (short)(SetMethodIndex - (ushort)lcd);
        GetMethodIndex = (short)(GetMethodIndex - (ushort)lcd);

        Name = StringProcessor.Decrypt(NameIndex);
    }
}
