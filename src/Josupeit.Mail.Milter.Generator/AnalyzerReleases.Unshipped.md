; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MILT001 | Josupeit.Mail.Milter | Error | A context implementing IMilterContext has to be partial to be generated into.
MILT002 | Josupeit.Mail.Milter | Error | The actions a context may yield could not be determined from this compilation.
