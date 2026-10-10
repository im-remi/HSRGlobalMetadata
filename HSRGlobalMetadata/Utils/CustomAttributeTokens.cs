
public static class CustomAttributeTokens {
    private const byte TypeDefTable = 0x02;
    private const byte FieldTable = 0x04;
    private const byte MethodTable = 0x06;
    private const byte ParameterTable = 0x08;
    private const byte EventTable = 0x14;
    private const byte PropertyTable = 0x17;
    private const byte AssemblyTable = 0x20;

    public static uint TypeToken(int typeDefinitionIndex) => MakeToken(TypeDefTable, typeDefinitionIndex);

    public static uint FieldToken(int fieldIndex) => MakeToken(FieldTable, fieldIndex);

    public static uint MethodToken(int methodIndex) => MakeToken(MethodTable, methodIndex);

    public static uint EventToken(int eventIndex) => MakeToken(EventTable, eventIndex);

    public static uint PropertyToken(int propertyIndex) => MakeToken(PropertyTable, propertyIndex);

    public static uint AssemblyToken(int assemblyIndex) => MakeToken(AssemblyTable, assemblyIndex);

    private static uint MakeToken(byte table, int zeroBasedIndex) {
        return ((uint)table << 24) | (uint)(zeroBasedIndex + 1);
    }
}
