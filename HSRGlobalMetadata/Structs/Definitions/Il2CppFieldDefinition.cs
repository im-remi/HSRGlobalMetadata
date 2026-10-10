using System.Text;
using HSRGlobalMetadata.Configuration;
using HSRGlobalMetadata.Structs.Runtime;
using HSRGlobalMetadata.Utils;

namespace HSRGlobalMetadata.Structs.Definitions;

public class Il2CppFieldDefinition : MetadataBase {
    private readonly int _index;
    private readonly int _fieldStart;
    private readonly int _fieldOffsetStart;

    public Il2CppFieldDefinition(int fieldStart, int fieldOffsetStart, int index) : base(MetadataContext.Instance.Metadata,
        MetadataHeader.Instance.FieldsOffset + index * MetadataConstants.FieldDefinitionSize) {
        _index = index;
        _fieldStart = fieldStart;
        _fieldOffsetStart = fieldOffsetStart;
        Populate();
    }
    
    [MetadataTag(0x00, MetadataOperation.ADD, 716162949)]
    public int NameIndex { get; private set; }
    
    [MetadataTag(0x04, MetadataOperation.ADD, 0)]
    public int TypeIndex { get; private set; }

    public uint Offset { get; private set; }

    public string Name;
    public Il2CppType Type;
    
    protected override void PostProcess() {
        var lcd = -1388221511 - 744344320 * (_index + 1954887780);

        Offset = BitConverter.ToUInt32(_bytes, MetadataHeader.Instance.FieldOffsetsOffset +
            (_index - _fieldStart + _fieldOffsetStart) * MetadataConstants.IndexSize);
        TypeIndex += lcd;
        NameIndex += lcd;

        Name = StringProcessor.Decrypt(NameIndex);
        Type = Il2CppType.FromIndex(TypeIndex);
    }

    public bool TryGetFieldStaticValue(out object? value) {
        value = null;
        if (((FieldAttributes)Type.Attrs & FieldAttributes.Literal) == 0)
            return false;
        if (!MetadataCache.FieldDefaultValues.TryGetValue(_index, out var entry) || entry.typeIndex == -1)
            return false;

        Il2CppType type = Il2CppType.FromIndex(entry.typeIndex);
        int baseOffset = checked(MetadataHeader.Instance.FieldAndParameterDefaultValueDataOffset + entry.dataOffset);
        byte[] metadata = MetadataContext.Instance.Metadata;

        value = type.Type switch {
            0x02 => metadata[baseOffset] != 0,
            0x03 => (char)BitConverter.ToUInt16(metadata, baseOffset),
            0x04 => unchecked((sbyte)metadata[baseOffset]),
            0x05 => metadata[baseOffset],
            0x06 => BitConverter.ToInt16(metadata, baseOffset),
            0x07 => BitConverter.ToUInt16(metadata, baseOffset),
            0x08 => BitConverter.ToInt32(metadata, baseOffset),
            0x09 => BitConverter.ToUInt32(metadata, baseOffset),
            0x0A => BitConverter.ToInt64(metadata, baseOffset),
            0x0B => BitConverter.ToUInt64(metadata, baseOffset),
            0x0C => BitConverter.ToSingle(metadata, baseOffset),
            0x0D => BitConverter.ToDouble(metadata, baseOffset),
            0x0E => ReadStringConstant(metadata, baseOffset),
            0x11 or 0x12 => BitConverter.ToInt32(metadata, baseOffset),
            0x18 => new IntPtr(BitConverter.ToInt64(metadata, baseOffset)),
            0x19 => new UIntPtr(BitConverter.ToUInt64(metadata, baseOffset)),
            0x1C or 0x1D => null,
            _ => null
        };
        return true;
    }

    private static string ReadStringConstant(byte[] metadata, int baseOffset) {
        int length = BitConverter.ToInt32(metadata, baseOffset);
        if (length < 0 || baseOffset + 4 > metadata.Length - length)
            throw new InvalidDataException($"String field constant at 0x{baseOffset:X} has invalid length {length}.");
        return Encoding.UTF8.GetString(metadata, baseOffset + 4, length);
    }
}
