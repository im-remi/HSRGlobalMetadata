namespace HSRGlobalMetadata.Utils;

public struct SectionTable {
    public uint virtualAddr;
    public uint sizeOfRawData;
    public uint ptrToRawData;
}

public static class PEHelper {
    public static ulong ImageBase;
    public static SectionTable[] sectionTables = [];
    
    public static ulong RvaToOffset(uint rva) {
        if (sectionTables == null || sectionTables.Length == 0)
            return 0;
        if (rva == 0) return 0;

        foreach (var section in sectionTables) {
            ulong sumAddr = section.virtualAddr + section.sizeOfRawData;
        
            if (rva >= section.virtualAddr && rva <= sumAddr) {
                ulong offset = section.ptrToRawData + (rva - section.virtualAddr);
                return offset;
            }
        }
        return ulong.MaxValue;
    }
    
    public static uint OffsetToRva(ulong fileOffset) {
        if (sectionTables == null || sectionTables.Length == 0)
            return 0;
        foreach (var section in sectionTables) {
            if (fileOffset >= section.ptrToRawData && fileOffset < section.ptrToRawData + section.sizeOfRawData) {
                return (uint)(section.virtualAddr + (fileOffset - section.ptrToRawData));
            }
        }
        return 0;
    }

    public static SectionTable[] ReadPEHeader(string dllPath) {
        using (var reader = new BinaryReader(File.Open(dllPath, FileMode.Open, FileAccess.Read, FileShare.Read))) {
            reader.BaseStream.Seek(0x3C, SeekOrigin.Begin);
            uint peOffset = reader.ReadUInt32();

            reader.BaseStream.Seek(peOffset, SeekOrigin.Begin);
            uint signature = reader.ReadUInt32();
            if (signature != 0x4550) {
                throw new Exception("Invalid PE signature");
            }

            _ = reader.ReadUInt16();
            ushort numberOfSections = reader.ReadUInt16();
            reader.ReadBytes(12);
            ushort optionalHeaderSize = reader.ReadUInt16();
            reader.ReadUInt16();

            long optionalHeaderStart = reader.BaseStream.Position;
            ushort magic = reader.ReadUInt16();

            ImageBase = magic switch {
                0x20B => ReadImageBase64(reader, optionalHeaderStart),
                0x10B => ReadImageBase32(reader, optionalHeaderStart),
                _ => throw new InvalidDataException(
                    $"Unsupported PE optional header magic: 0x{magic:X}")
            };

            reader.BaseStream.Seek(optionalHeaderStart + optionalHeaderSize, SeekOrigin.Begin);

            var sections = new SectionTable[numberOfSections];
            for (int i = 0; i < numberOfSections; i++) {
                reader.ReadBytes(8);
                reader.ReadUInt32();
                sections[i].virtualAddr = reader.ReadUInt32();
                sections[i].sizeOfRawData = reader.ReadUInt32();
                sections[i].ptrToRawData = reader.ReadUInt32();
                reader.ReadBytes(16);
            }

            sectionTables = sections;

            return sections;
        }
    }

    private static ulong ReadImageBase64( BinaryReader reader, long optionalHeaderStart) {
        reader.BaseStream.Seek(optionalHeaderStart + 0x18, SeekOrigin.Begin);
        return reader.ReadUInt64();
    }
    
    private static ulong ReadImageBase32(BinaryReader reader, long optionalHeaderStart) {
        reader.BaseStream.Seek(optionalHeaderStart + 0x1C, SeekOrigin.Begin);
        return reader.ReadUInt32();
    }
    
    public static byte[] ReadBytes(string path, long offset, int count) {
        byte[] buffer = new byte[count];

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        fs.Seek(offset, SeekOrigin.Begin);

        int read = fs.Read(buffer, 0, count);
        if (read != count)
            throw new EndOfStreamException();

        return buffer;
    }
}
