using System;

namespace Josupeit.Mail.Milter.Link;

// These values map to the SMFIF_ constants.
[Flags]
public enum ActionDiscriminator : uint
{
    None = 0x00,
    AddRecipients = 0x04, // SMFIF_ADDRCPT
    DeleteRecipients = 0x08, // SMFIF_DELRCPT
    AddHeaders = 0x01, // SMFIF_ADDHDRS
    ChangeOrDeleteHeaders = 0x10, // SMFIF_CHGHDRS
    ChangeBodyChunks = 0x02, // SMFIF_CHGBODY
    QuarantineMessage = 0x20, // SMFIF_QUARANTINE
    All = AddHeaders | ChangeBodyChunks | AddRecipients | DeleteRecipients | ChangeOrDeleteHeaders | QuarantineMessage,
}