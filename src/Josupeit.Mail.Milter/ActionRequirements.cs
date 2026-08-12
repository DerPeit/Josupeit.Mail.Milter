// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;

namespace Josupeit.Mail.Milter;

[Flags]
public enum ActionRequirements
{
    None = 0,
    AddRecipients = 1,
    DeleteRecipients = 2,
    AddHeaders = 4,
    ChangeHeaders = 8,
    DeleteHeaders = 16,
    ChangeBody = 32,
    QuarantineMessage = 64,
    All = AddRecipients | DeleteRecipients | AddHeaders | ChangeHeaders | DeleteHeaders | ChangeBody | QuarantineMessage,
}