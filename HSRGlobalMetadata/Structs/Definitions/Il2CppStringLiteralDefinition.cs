using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata.Structs.Definitions;

public class Il2CppStringLiteral : MetadataBase {
  private readonly int _index;

  public Il2CppStringLiteral(int index): base(MetadataContext.Instance.Metadata,
    MetadataHeader.Instance.StringLiteralOffset + MetadataConstants.IndexSize * index) {
    _index = index;
    Populate();
  }

  [MetadataTag(0x00)]
  public int EntryValue { get; private set; }

  [MetadataTag(0x04)]
  public int NextEntryValue { get; private set; }

  public uint DataOffset;
  public uint StringLength;
  public byte[] RawBytes;
  public String Value;

  protected override void PostProcess() {
    ulong keyStep = 0x32C1CF25BB14UL;
    ulong enc = keyStep * (ulong)_index;
    ulong keyBase = 0x1F0FE259CF0538UL;

    ulong key1 = (604527770UL * ((enc + keyBase) >> 14)) >> 8;
    ulong key2 = (604527770UL * ((enc + keyBase + keyStep) >> 14)) >> 8;

    int dataOffset = (int)((ulong)EntryValue - key1);
    int nextDataOffset = (int)((ulong)NextEntryValue - key2);

    DataOffset = (uint)(dataOffset + 1004094107L + MetadataHeader.Instance.StringLiteralDataOffset);
    StringLength = (uint)(nextDataOffset - dataOffset);
    
    RawBytes = StringLiteralProcessor.Decrypt((uint)_index, MetadataContext.Instance.Metadata, DataOffset, StringLength);

    Value = System.Text.Encoding.UTF8.GetString(RawBytes);
  }
}
