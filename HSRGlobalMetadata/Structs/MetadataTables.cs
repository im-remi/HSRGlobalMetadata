using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata.Structs;

public class MetadataTables: MetadataBase {
  private static MetadataTables? _instance;
  public static MetadataTables Instance => _instance ?? throw new Exception("MetadataTables not initialized");

  [MetadataTag(0x10)]
  public int StringLiteralRva { get; private set; }

  [MetadataTag(0x2C, MetadataOperation.XOR, 0xBD08DC8)]
  public int StringLiteralCount { get; private set; }

  public MetadataTables(byte[] bytes): base(bytes) {
    Populate();
  }

  public static void Initialize(string gameAssemblyPath) {
    long metadataTablesPtr = RegisterPointersFunction.Initialize(gameAssemblyPath).GetMetadataTables();
    byte[] bytes = new ArraySegment<byte>(MetadataContext.Instance.GameAssembly, (int)metadataTablesPtr, 0x68).ToArray();
    _instance = new MetadataTables(bytes);
  }

  protected override void PostProcess() {
    StringLiteralRva = unchecked(StringLiteralRva - (int)PEHelper.ImageBase);
  }
}
