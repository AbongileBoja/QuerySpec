; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID    | Category             | Severity | Notes
-----------|----------------------|----------|------------------------------------------------------------
QSPEC0001  | QuerySpec.Migration  | Warning  | Use GeoCoordinate instead of GeoLocation.Latitude/.Longitude
QSPEC0002  | QuerySpec.Migration  | Warning  | Use FilterSpec instead of AdvancedFilterExpression
QSPEC0003  | QuerySpec.Migration  | Warning  | Use ICacheStore instead of ICacheProvider GetAsync/SetAsync
