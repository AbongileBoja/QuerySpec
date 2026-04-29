# QSPEC0013: Use GenerateGdprExportAsync instead of GenerateGDPRExportAsync (class, no CancellationToken)

| Property        | Value                                                                                          |
| --------------- | ---------------------------------------------------------------------------------------------- |
| Diagnostic ID   | `QSPEC0013`                                                                                    |
| Severity        | Warning (`[Obsolete(error: false)]`)                                                           |
| Introduced      | QuerySpec 4.0.0                                                                                |
| Deprecated API  | `QuerySpec.Core.Auditing.ComplianceExporter.GenerateGDPRExportAsync(string, string, Stream)`   |
| Replacement     | `QuerySpec.Core.Auditing.ComplianceExporter.GenerateGdprExportAsync(string, string, Stream)`   |

## Cause

A program calls the concrete `ComplianceExporter.GenerateGDPRExportAsync(string, string, Stream)` method. The Framework Design Guidelines (`Capitalization Conventions for Acronyms`) require acronyms longer than two letters to use PascalCase. The replacement `GenerateGdprExportAsync` carries identical semantics and dispatches to the same underlying serialisation logic.

## Replacement

```csharp
// Before
var exporter = new ComplianceExporter(auditReader);
await exporter.GenerateGDPRExportAsync(userId, tenantId, stream);

// After
var exporter = new ComplianceExporter(auditReader);
await exporter.GenerateGdprExportAsync(userId, tenantId, stream);
```

## Suppression

```csharp
#pragma warning disable QSPEC0013
await exporter.GenerateGDPRExportAsync(userId, tenantId, stream);
#pragma warning restore QSPEC0013
```

## See also

- Tracking issue: [#168](https://github.com/AbongileBoja/QuerySpec/issues/168)
