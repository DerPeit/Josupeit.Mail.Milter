// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

namespace Microsoft.CodeAnalysis;

// Normally the compiler synthesises this when a language feature needs a type of its own. Declaring it
// by hand makes the same machinery available to the marker attributes next to it: a type marked with it
// is not imported by a referencing compilation at all, so the markers can travel inside the library
// without appearing in anyone's IntelliSense, and without two copies of them ever being ambiguous.
[System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = false, Inherited = false)]
[Embedded]
internal sealed class EmbeddedAttribute : System.Attribute
{
}
