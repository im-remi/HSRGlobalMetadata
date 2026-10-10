namespace HSRGlobalMetadata.DummyDll.Generation;

public sealed record DummyDllDiagnostic(
    string Severity,
    string Code,
    string Message,
    int? ImageIndex = null,
    int? TypeDefinitionIndex = null,
    string? MemberKind = null,
    int? MemberIndex = null
);

public sealed class DummyDllGenerationReport {
    private readonly List<DummyDllDiagnostic> _diagnostics = [];

    public int AssemblyCount { get; internal set; }
    public int TypeCount { get; internal set; }
    public int FieldCount { get; internal set; }
    public int SerializableFieldCount { get; internal set; }
    public int SerializableFieldPlaceholderCount { get; internal set; }
    public int PropertyCount { get; internal set; }
    public int AccessorCount { get; internal set; }
    public int MethodCount { get; internal set; }
    public int EventCount { get; internal set; }
    public int AddressedMethodCount { get; internal set; }
    public int GenericConstraintCount { get; internal set; }
    public int PublicFieldCount { get; internal set; }
    public int PublicPropertyCount { get; internal set; }
    public int PublicMethodCount { get; internal set; }
    public int PublicEventCount { get; internal set; }
    public int PublicMemberPlaceholderCount { get; internal set; }
    public int PreparedPublicMemberCount =>
        PublicFieldCount + PublicPropertyCount + PublicMethodCount + PublicEventCount;
    public int SyntheticBackingFieldCount { get; internal set; }
    public int PlaceholderTypeCount { get; internal set; }
    public IReadOnlyList<DummyDllDiagnostic> Diagnostics => _diagnostics;

    internal void Add(DummyDllDiagnostic diagnostic) => _diagnostics.Add(diagnostic);
}

public sealed record DummyDllExportResult(
    IReadOnlyList<string> AssemblyPaths,
    string ReportPath,
    DummyDllGenerationReport Report
);
