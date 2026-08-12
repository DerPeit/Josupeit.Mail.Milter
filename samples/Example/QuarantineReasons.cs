// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using Josupeit.Mail.Milter;

namespace Example;

// Deliberately hands back the abstract ModificationAction: nothing at the call site says which
// modification this is, so the generator only learns that the filter quarantines by reading this body.
internal static class QuarantineReasons
{
    public static ModificationAction ForSuspiciousBody()
    {
        return ModificationActions.QuarantineMessage("the body offered a free cruise to nowhere");
    }
}
