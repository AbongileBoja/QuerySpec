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

Implementations that override the new-name overload satisfy the interface contract.

In v4.x, the old-name member was abstract on the interface; existing implementations continued to compile and consumers saw a warning that pointed at this document.

## v5.0 binary break

As of v5.0, `GenerateGdprExportAsync(..., CancellationToken)` is the abstract interface member. The obsolete acronym-form members (`GenerateGDPRExportAsync` no-CT and CT) are now default-interface-methods that delegate forward to the new-name abstract.

Implications for v4.x consumers upgrading to v5.0:

- Customer code that implements `IComplianceExporter` by overriding only `GenerateGDPRExportAsync` no longer satisfies the interface contract. Such implementations must override `GenerateGdprExportAsync(..., CancellationToken)` instead.
- Recompilation against the v5.0 reference assembly will surface this as a missing-implementation error on the abstract `GenerateGdprExportAsync(..., CancellationToken)` member.
- v4.x-built binaries that implemented only the acronym-form member will throw `TypeLoadException` when loaded against the v5.0 interface, because the abstract slot is now `GenerateGdprExportAsync`.

## Suppression

```csharp
#pragma warning disable QSPEC0011
exporter.GenerateGDPRExportAsync(userId, tenantId, stream);
#pragma warning restore QSPEC0011
```

## See also

- Tracking issue: [#168](https://github.com/AbongileBoja/QuerySpec/issues/168)
- v5.0 carve-out: [#255](https://github.com/AbongileBoja/QuerySpec/issues/255)
