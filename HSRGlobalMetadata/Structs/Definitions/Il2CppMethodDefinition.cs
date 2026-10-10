using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Structs.Runtime;
using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata.Structs.Definitions;

public class Il2CppMethodDefinition : MetadataBase {
    private readonly int _index;

    public Il2CppMethodDefinition(int index) : base(MetadataContext.Instance.Metadata,
        MetadataHeader.Instance.MethodOffset + index * MetadataConstants.MethodDefinitionSize) {
        _index = index;
        Populate();
    }
    
    [MetadataTag(0x00, MetadataOperation.XOR, 0xE714BC1)]
    public int NameIndex { get; set; }
    
    [MetadataTag(0x04, MetadataOperation.XOR, 0x9889B8)]
    public int ParametersStart { get; set; }
    
    [MetadataTag(0x08, MetadataOperation.SUB, 1698564893)]
    public int ReturnTypeIndex { get; set; }
    
    [MetadataTag(0x10, MetadataOperation.XOR, 0x2A5FABE8)]
    public int DeclaringTypeIndex { get; set; }

    [MetadataTag(0x14, MetadataOperation.SUB, 30979)]
    public short GenericContainerIndex { get; set; }
    
    [MetadataTag(0x18, MetadataOperation.XOR, 0xA8)]
    public byte ParametersCount { get; set; }

    public uint Flags { get; private set; }
    public long MethodPointer { get; private set; }

    public Il2CppType ReturnType { get; private set; }
    public Il2CppTypeDefinition DeclaringType { get; private set; }

    public string Name;

 
    protected override void PostProcess() {
        ulong lcd = ((1410124276UL * ((738455933UL * ((ulong)(12769UL * (ulong)_index) ^ 0x33914937UL)) >> 23)) >> 21) + 1908176993;
        
        uint flagsEnc = BitConverter.ToUInt32(_bytes, _baseOffset + 0x0C);
        uint shuffled = (flagsEnc >> 16) | (flagsEnc << 16);
        Flags = shuffled ^ (uint)((ushort)(lcd & 0xFFFF) | ((ushort)(lcd & 0xFFFF) << 16)) ^ 0x09F73733;

        var val = PEHelper.RvaToOffset((uint)CodeRegistration.Instance.MethodPointer);
        MethodPointer = BitConverter.ToInt64(MetadataContext.Instance.GameAssembly,
            (int)val + _index * MetadataConstants.PointerSize);
        
        NameIndex ^= (int)lcd;
        ReturnTypeIndex ^= (int)lcd;
        ParametersStart ^= (int)lcd;
        DeclaringTypeIndex ^= (int)lcd;
        GenericContainerIndex = (short)(GenericContainerIndex ^ (int)lcd);
        ParametersCount ^= (byte)lcd;

        Name = StringProcessor.Decrypt(NameIndex);
        if (ReturnTypeIndex != -1)
            ReturnType = Il2CppType.FromIndex(ReturnTypeIndex);
        
        if (DeclaringTypeIndex != -1)
            DeclaringType = MetadataCache.TypeDefs[DeclaringTypeIndex];
    }
}
