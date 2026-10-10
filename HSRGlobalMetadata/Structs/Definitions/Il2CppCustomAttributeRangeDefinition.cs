
using HSRGlobalMetadata;
using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Structs;
using HSRGlobalMetadata.Utils;

public class Il2CppCustomAttributeRangeDefinition : MetadataBase {
    [MetadataTag(0x00)]
    public uint EncodedRange { get; set; }

    [MetadataTag(0x04)]
    public uint Token { get; set; }

    public int Count;
    public int Start;

    public Il2CppCustomAttributeRangeDefinition(int index) : base(MetadataContext.Instance.Metadata, MetadataHeader.Instance.CustomAttributeRangeOffset + index * MetadataConstants.CustomAttributeRangeSize) {
        Populate();
    }

    protected override void PostProcess() {
        Count = (int)(EncodedRange >> 24);
        Start = (int)(EncodedRange & 0x00FFFFFF);
    }
}