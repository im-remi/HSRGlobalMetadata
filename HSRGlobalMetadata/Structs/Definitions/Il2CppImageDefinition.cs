using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata.Structs.Definitions;

public class Il2CppImageDefinition : MetadataBase {
    private readonly int _index;

    public Il2CppImageDefinition(int index) : base(MetadataContext.Instance.StartupMetadata,
        MetadataHeader.Instance.ImagesOffset + MetadataConstants.ImageDefinitionSize * index) {
        _index = index; 
        Populate();
    }

    [MetadataTag(0x00, MetadataOperation.SUB, 1325708857)]
    public int AssemblyIndex { get; set; }

    [MetadataTag(0x04, MetadataOperation.XOR, 0x10FEA394)]
    public int TypeCount { get; private set; }

    [MetadataTag(0x08, MetadataOperation.SUB, 314160849)]
    public int ExportedTypeCount { get; set; }
    
    [MetadataTag(0x0C, MetadataOperation.XOR, 0x4D648371)]
    public int NameIndex { get; set; }
    
    [MetadataTag(0x14, MetadataOperation.XOR, 0x7BABEEA0)]
    public int TypeStart { get; private set; }
    
    [MetadataTag(0x20, MetadataOperation.XOR, 0x7D0A6E8E)]
    public int ExportedTypeStart { get; set; }
    
    [MetadataTag(0x24, MetadataOperation.SUB, 190034452)]
    public int Token { get; set; }
    
    public string Name { get; private set; }

    protected override void PostProcess() {
        int lcd = (((302843651 * ((57468 * _index) ^ 0x7538159E)) ^ 0x6032C9D3) + 784007301);

        NameIndex ^= lcd;
        AssemblyIndex ^= lcd;
        Token ^= lcd;
        ExportedTypeCount ^= lcd;
        ExportedTypeStart ^= lcd;
        TypeCount ^= lcd;
        TypeCount ^= 0x7C06D18C;
        TypeStart ^= lcd;
        TypeStart ^= 0x235AEAF5;

        if (NameIndex != -1)
            Name = StringProcessor.Decrypt(NameIndex);
    }
}
