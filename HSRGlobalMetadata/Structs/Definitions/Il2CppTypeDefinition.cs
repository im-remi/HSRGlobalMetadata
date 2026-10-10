using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Structs.Runtime;
using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata.Structs.Definitions;

public class Il2CppTypeDefinition : MetadataBase {
    private readonly int _index;

    public Il2CppTypeDefinition(int index) : base(MetadataContext.Instance.Metadata,
        MetadataHeader.Instance.TypeDefinitionsOffset + index * MetadataConstants.TypeDefinitionSize) {
        _index = index;
        Populate();
    }

    [MetadataTag(0x04, MetadataOperation.SUB, 1224299847)]
    public int ParentIndex { get; set; }

    [MetadataTag(0x08, MetadataOperation.XOR, 0x1A7AF5FE)]
    public int MethodStart { get; set; }

    [MetadataTag(0x0C, MetadataOperation.SUB, 472568492)]
    public int DeclaringTypeIndex { get; set; }

    [MetadataTag(0x14, MetadataOperation.SUB, 17986448)]
    public int Flags { get; set; }

    [MetadataTag(0x1C, MetadataOperation.SUB, 970393327)]
    public int PropertyStart { get; set; }

    [MetadataTag(0x20, MetadataOperation.SUB, 1954887780)]
    public int FieldStart { get; set; }

    [MetadataTag(0x24, MetadataOperation.SUB, 237818487)]
    public int NamespaceIndex { get; set; }

    [MetadataTag(0x28, MetadataOperation.SUB, 369268488)]
    public int TypeNameIndex { get; set; }

    [MetadataTag(0x30, MetadataOperation.XOR, 0x6ECD)]
    public short EventStart { get; set; }

    [MetadataTag(0x32, MetadataOperation.ADD, 17485)]
    public ushort FieldCount { get; set; }

    [MetadataTag(0x34, MetadataOperation.ADD, 24467)]
    public ushort MethodCount { get; set; }

    [MetadataTag(0x36, MetadataOperation.XOR, 0xC28C)]
    public ushort InterfaceStart { get; set; }

    [MetadataTag(0x38)]
    public int Bitfield { get; set; }

    [MetadataTag(0x3A, MetadataOperation.XOR, 0xB2C0)]
    public short NestedTypeStart { get; set; }
    
    [MetadataTag(0x3C, MetadataOperation.SUB, 44028)]
    public short GenericContainerIndex { get; set; }

    [MetadataTag(0x40, MetadataOperation.XOR, 0x73)]
    public byte EventCount { get; set; }

    [MetadataTag(0x41, MetadataOperation.ADD, 31)]
    public byte PropertyCount { get; set; }

    [MetadataTag(0x43, MetadataOperation.ADD, 4)]
    public byte NestedTypeCount { get; set; }

    [MetadataTag(0x44, MetadataOperation.XOR, 0xC7)]
    public byte InterfaceCount { get; set; }


    public string Namespace { get; private set; } = "";
    public string Name { get; private set; } = "";
    public Il2CppType Parent { get; private set; }

    protected override void PostProcess() {
        if (ParentIndex != -1)
            Parent = Il2CppType.FromIndex(ParentIndex);

        Namespace = StringProcessor.Decrypt(NamespaceIndex);
        Name = StringProcessor.Decrypt(TypeNameIndex);
    }

    public int GetFieldOffsetIndex() {
        int mappedIndex = BitConverter.ToInt32(MetadataContext.Instance.Metadata,
            MetadataHeader.Instance.TypeIndexMapOffset + MetadataConstants.IndexSize * _index);
        int fieldOffsetEntry = BitConverter.ToInt32(MetadataContext.Instance.Metadata, MetadataHeader.Instance.IdxTableBaseOffset + 12 * mappedIndex + 8);

        return fieldOffsetEntry;
    }
}
