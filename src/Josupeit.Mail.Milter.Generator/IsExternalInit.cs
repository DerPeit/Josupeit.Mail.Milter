// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System.ComponentModel;

namespace System.Runtime.CompilerServices;

// A record's init accessors compile down to a reference to this type, and netstandard2.0 - which a
// Roslyn component has to target - does not carry it. The compiler is happy with any definition, so the
// project supplies its own.
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit
{
}
