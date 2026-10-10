using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata.Structs.Definitions;

public class Il2CppGenericParameterDefinition : MetadataBase {
    private readonly int _index;

    [MetadataTag(0x00)]
    public int NameIndex { get; set; }

    [MetadataTag(0x04, MetadataOperation.XOR, 0x69B4)]
    public short ConstraintsStart { get; set; }

    [MetadataTag(0x06, MetadataOperation.XOR, 0xFD1)]
    public short ConstraintsCount { get; set; }

    public string Name { get; private set; }
    
    public List<string> Constraints { get; } = new();

    public Il2CppGenericParameterDefinition(int index) : base(MetadataContext.Instance.Metadata,
        MetadataHeader.Instance.GenericParametersOffset + MetadataConstants.GenericParameterDefinitionSize * index) {
        _index = index;
        Populate();
    }

    protected override void PostProcess() {
        ulong paramLcd = (1252900171UL * ((((ulong)(0x617FE3CC452CUL * (ulong)_index + 0x9DC5DB71F0EB440UL) >> 9) + 718849585) ^ 0x5278374D)) >> 15;

        NameIndex -= (int)paramLcd + 1149796643;
        ConstraintsStart = (short)(ConstraintsStart - (ushort)paramLcd);
        ConstraintsCount = (short)(ConstraintsCount - (ushort)paramLcd);

        Name = StringProcessor.Decrypt(NameIndex);

        for (int c = 0; c < ConstraintsCount; c++) {
            var constraint = new Il2CppGenericParameterConstraintDefinition(ConstraintsStart + c);
            Constraints.Add(constraint.TypeName);
        }
    }
}
