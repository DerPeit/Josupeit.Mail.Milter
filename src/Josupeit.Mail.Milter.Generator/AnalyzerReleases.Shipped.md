; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.1

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MILT001 | Josupeit.Mail.Milter | Error | A context implementing IMilterContext has to be partial to be generated into.
MILT002 | Josupeit.Mail.Milter | Error | The actions a context may yield could not be determined from this compilation.

## Release 0.2

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MILT003 | Josupeit.Mail.Milter | Error | The macros a context reads could not be determined from this compilation.
