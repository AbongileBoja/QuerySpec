# QSPEC0012: Use GenerateGdprExportAsync instead of GenerateGDPRExportAsync (interface, with CancellationToken)

| Property        | Value                                                                                                                |
| --------------- | -------------------------------------------------------------------------------------------------------------------- |
| Diagnostic ID   | `QSPEC0012`                                                                                                          |
| Severity        | Warning (`[Obsolete(error: false)]`)                                                                                 |
| Introduced      | QuerySpec 4.0.0                                                                                                      |
| Deprecated API  | `QuerySpec.Core.Auditing.IComplianceExporter.GenerateGDPRExportAsync(string, string, Stream, CancellationToken)`     |
| Replacement     | `QuerySpec.Core.Auditing.IComplianceExporter.GenerateGdprExportAsync(string, string, Stream, CancellationToken)`     |

## Cause

A program calls or implements the cancellation-token-accepting interface member `IComplianceExporter.GenerateGDPRExportAsync(string, string, Stream, CancellationToken)`. The Framework Design Guidelines (`Capitalization Conventions for Acronyms`) require acronyms longer than two letters to use PascalCase. The replacement `GenerateGdprExportAsync` carries identical semantics.

## Replacement

```csharp
// Before
await exporter.GenerateGDPRExportAsync(userId, tenantId, stream, cancellationToken);

// After
await exporter.GenerateGdprExportAsync(userId, tenantId, stream, cancellationToken);
```

The old-name CancellationToken overload is a default-interface-method that delegates forward to the new-name CancellationToken overload, so callers and implementers can move at their own pace.

## v5.0 status

As of v5.0, `GenerateGdprExportAsync(..., CancellationToken)` is the abstract interface member. This obsolete overload is a default-interface-method bridge that forwards to the new-name abstract — its runtime behaviour is unchanged. The bridge is retained for source compatibility with v4.x callers; it remains marked obsolete and will be removed in a future major release. See [QSPEC0011](QSPEC0011.md) for the v5.0 binary-break implications.

## Suppression

```csharp
#pragma warning disable QSPEC0012
await exporter.GenerateGDPRExportAsync(userId, tenantId, stream, cancellationToken);
#pragma warning restore QSPEC0012
```

## See also

- Tracking issue: [#168](https://github.com/AbongileBoja/QuerySpec/issues/168)
- v5.0 carve-out: [#255](https://github.com/AbongileBoja/QuerySpec/issues/255)
