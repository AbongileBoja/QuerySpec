# QSPEC0011: Use GenerateGdprExportAsync instead of GenerateGDPRExportAsync (interface, no CancellationToken)

| Property        | Value                                                                                            |
| --------------- | ------------------------------------------------------------------------------------------------ |
| Diagnostic ID   | `QSPEC0011`                                                                                      |
| Severity        | Warning (`[Obsolete(error: false)]`)                                                             |
| Introduced      | QuerySpec 4.0.0                                                                                  |
| Deprecated API  | `QuerySpec.Core.Auditing.IComplianceExporter.GenerateGDPRExportAsync(string, string, Stream)`    |
| Replacement     | `QuerySpec.Core.Auditing.IComplianceExporter.GenerateGdprExportAsync(string, string, Stream)`    |

## Cause

A program calls or implements the interface member `IComplianceExporter.GenerateGDPRExportAsync(string, string, Stream)`. The Framework Design Guidelines (`Capitalization Conventions for Acronyms`) require acronyms longer than two letters to use PascalCase, so `GDPR` becomes `Gdpr`. The replacement `GenerateGdprExportAsync` carries identical semantics.

## Replacement

```csharp
// Before
public class MyExporter : IComplianceExporter
{
    public Task GenerateGDPRExportAsync(string userId, string tenantId, Stream outputStream)
        => /* ... */;
}

// After
public class MyExporter : IComplianceExporter
{
    public Task GenerateGdprExportAsync(string userId, string tenantId, Stream outputStream, CancellationToken cancellationToken = default)
        => /* ... */;
}
```

Implementations that override the new-name overload satisfy the interface contract. The old-name member remains abstract on the interface in the v4.x line, so existing implementations continue to compile; consumers see a warning that points at this document.

## v5.0 plan

In v5.0, `GenerateGdprExportAsync(..., CancellationToken)` will become the abstract interface member and the obsolete acronym-form members will be demoted to default-interface-method bridges. See the v5.0 tracker for migration details: [#255](https://github.com/AbongileBoja/QuerySpec/issues/255).

## Suppression

```csharp
#pragma warning disable QSPEC0011
exporter.GenerateGDPRExportAsync(userId, tenantId, stream);
#pragma warning restore QSPEC0011
```

## See also

- Tracking issue: [#168](https://github.com/AbongileBoja/QuerySpec/issues/168)
- v5.0 carve-out: [#255](https://github.com/AbongileBoja/QuerySpec/issues/255)
