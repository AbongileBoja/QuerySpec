# QSPEC0014: Use GenerateGdprExportAsync instead of GenerateGDPRExportAsync (class, with CancellationToken)

| Property        | Value                                                                                                              |
| --------------- | ------------------------------------------------------------------------------------------------------------------ |
| Diagnostic ID   | `QSPEC0014`                                                                                                        |
| Severity        | Warning (`[Obsolete(error: false)]`)                                                                               |
| Introduced      | QuerySpec 4.0.0                                                                                                    |
| Deprecated API  | `QuerySpec.Core.Auditing.ComplianceExporter.GenerateGDPRExportAsync(string, string, Stream, CancellationToken)`    |
| Replacement     | `QuerySpec.Core.Auditing.ComplianceExporter.GenerateGdprExportAsync(string, string, Stream, CancellationToken)`    |

## Cause

A program calls the concrete `ComplianceExporter.GenerateGDPRExportAsync(string, string, Stream, CancellationToken)` method. The Framework Design Guidelines (`Capitalization Conventions for Acronyms`) require acronyms longer than two letters to use PascalCase. The replacement `GenerateGdprExportAsync` carries identical semantics and propagates the cancellation token through to `JsonSerializer.SerializeAsync`.

## Replacement

```csharp
// Before
await exporter.GenerateGDPRExportAsync(userId, tenantId, stream, cancellationToken);

// After
await exporter.GenerateGdprExportAsync(userId, tenantId, stream, cancellationToken);
```

## Suppression

```csharp
#pragma warning disable QSPEC0014
await exporter.GenerateGDPRExportAsync(userId, tenantId, stream, cancellationToken);
#pragma warning restore QSPEC0014
```

## See also

- Tracking issue: [#168](https://github.com/AbongileBoja/QuerySpec/issues/168)
