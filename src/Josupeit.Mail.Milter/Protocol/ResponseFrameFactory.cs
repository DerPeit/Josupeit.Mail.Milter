// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Josupeit.Mail.Milter.Link;

namespace Josupeit.Mail.Milter.Protocol;

/// <summary>
/// Encodes response packets into the frames that carry them on the wire.
/// </summary>
/// <remarks>
/// The counterpart of <see cref="RequestPacketFactory"/>. The encoding is done by an internal
/// <see cref="ResponsePacketVisitor"/>, so each packet kind is handled by the method named for it rather
/// than by a type switch.
/// </remarks>
public class ResponseFrameFactory : IResponseFrameFactory
{
    /// <inheritdoc />
    public ResponseFrame CreateResponseFrame(ResponsePacket response)
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));

        return new ResponseFrameCreatingVisitor().CreateResponseFrame(response);
    }
}

file sealed class ResponseFrameCreatingVisitor : ResponsePacketVisitor
{
    private ResponseFrame _responseFrame;

    public ResponseFrame CreateResponseFrame(ResponsePacket responsePacket)
    {
        if (!VisitResponsePacket(responsePacket))
            throw new ArgumentException($"Unable to create a {nameof(ResponseFrame)} from the supplied {nameof(ResponsePacket)}.", nameof(responsePacket));

        return _responseFrame;
    }

    protected internal override bool VisitAcceptMessageResponsePacket(AcceptMessageResponsePacket responsePacket)
    {
        _responseFrame = new(ResponseCode.AcceptMessage);

        return true;
    }

    protected internal override bool VisitAddHeaderResponsePacket(AddHeaderResponsePacket responsePacket)
    {
        var nameLength = Encoding.Latin1.GetByteCount(responsePacket.Name);
        const int END_OF_NAME_MARK_LENGTH = 1;
        var valueLength = Encoding.Latin1.GetByteCount(responsePacket.Value);
        const int END_OF_VALUE_MARK_LENGTH = 1;
        var blockLength = nameLength + END_OF_NAME_MARK_LENGTH + valueLength + END_OF_VALUE_MARK_LENGTH;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(blockLength);

        try
        {
            var memoryBlock = memoryOwner.Memory[..blockLength];

            const int NAME_OFFSET = 0;
            var endOfNameMarkOffset = NAME_OFFSET + nameLength;
            var valueOffset = endOfNameMarkOffset + END_OF_NAME_MARK_LENGTH;
            var endOfValueMarkOffset = valueOffset + valueLength;

            var nameMemoryBlock = memoryBlock.Slice(NAME_OFFSET, nameLength);
            var endOfNameMarkMemoryBlock = memoryBlock.Slice(endOfNameMarkOffset, END_OF_NAME_MARK_LENGTH);
            var valueMemoryBlock = memoryBlock.Slice(valueOffset, valueLength);
            var endOfValueMarkMemoryBlock = memoryBlock.Slice(endOfValueMarkOffset, END_OF_VALUE_MARK_LENGTH);

            // I cannot find whether the memory block is guaranteed to be zero-ed.
            endOfNameMarkMemoryBlock.Span[0] = endOfValueMarkMemoryBlock.Span[0] = 0;

            Encoding.Latin1.GetBytes(responsePacket.Name, nameMemoryBlock.Span);
            Encoding.Latin1.GetBytes(responsePacket.Value, valueMemoryBlock.Span);

            _responseFrame = new(ResponseCode.AddHeader, memoryOwner, ..blockLength);

            return true;
        }
        catch
        {
            memoryOwner.Dispose();

            throw;
        }
    }

    protected internal override bool VisitAddRecipientResponsePacket(AddRecipientResponsePacket responsePacket)
    {
        var recipientLength = Encoding.Latin1.GetByteCount(responsePacket.Recipient);
        const int END_OF_RECIPIENT_MARK_LENGTH = 1;

        // Two response codes out of one packet. Arguments make it SMFIR_ADDRCPT_PAR, which the MTA
        // grants as a right of its own, so a recipient carrying none has to go out as a plain
        // SMFIR_ADDRCPT: sending an ADDRCPT_PAR with an empty field instead would have the MTA refuse
        // the recipient of any filter that only asked for AddRecipients.
        var hasArguments = !string.IsNullOrEmpty(responsePacket.ESmtpArguments);
        var argumentsLength = hasArguments ? Encoding.Latin1.GetByteCount(responsePacket.ESmtpArguments!) : 0;
        var endOfArgumentsMarkLength = hasArguments ? 1 : 0;
        var blockLength = recipientLength + END_OF_RECIPIENT_MARK_LENGTH + argumentsLength + endOfArgumentsMarkLength;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(blockLength);

        try
        {
            var memoryBlock = memoryOwner.Memory[..blockLength];

            const int RECIPIENT_OFFSET = 0;
            var endOfRecipientMarkOffset = RECIPIENT_OFFSET + recipientLength;
            var argumentsOffset = endOfRecipientMarkOffset + END_OF_RECIPIENT_MARK_LENGTH;

            var recipientMemoryBlock = memoryBlock.Slice(RECIPIENT_OFFSET, recipientLength);
            var endOfRecipientMarkMemoryBlock = memoryBlock.Slice(endOfRecipientMarkOffset, END_OF_RECIPIENT_MARK_LENGTH);

            endOfRecipientMarkMemoryBlock.Span[0] = 0;

            Encoding.Latin1.GetBytes(responsePacket.Recipient, recipientMemoryBlock.Span);

            if (hasArguments)
            {
                var argumentsMemoryBlock = memoryBlock.Slice(argumentsOffset, argumentsLength);
                var endOfArgumentsMarkMemoryBlock = memoryBlock.Slice(argumentsOffset + argumentsLength, endOfArgumentsMarkLength);

                endOfArgumentsMarkMemoryBlock.Span[0] = 0;

                Encoding.Latin1.GetBytes(responsePacket.ESmtpArguments!, argumentsMemoryBlock.Span);
            }

            var responseCode = hasArguments ? ResponseCode.AddRecipientWithParameters : ResponseCode.AddRecipient;

            _responseFrame = new(responseCode, memoryOwner, ..blockLength);

            return true;
        }
        catch
        {
            memoryOwner.Dispose();

            throw;
        }
    }

    protected internal override bool VisitChangeOrDeleteHeaderResponsePacket(ChangeOrDeleteHeaderResponsePacket responsePacket)
    {
        const int INDEX_LENGTH = sizeof(uint);
        var nameLength = Encoding.Latin1.GetByteCount(responsePacket.Name);
        const int END_OF_NAME_MARK_LENGTH = 1;
        var valueLength = Encoding.Latin1.GetByteCount(responsePacket.Value);
        const int END_OF_VALUE_MARK_LENGTH = 1;
        var blockLength = INDEX_LENGTH + nameLength + END_OF_NAME_MARK_LENGTH + valueLength + END_OF_VALUE_MARK_LENGTH;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(blockLength);

        try
        {
            var memoryBlock = memoryOwner.Memory[..blockLength];

            const int INDEX_OFFSET = 0;
            var nameOffset = INDEX_OFFSET + INDEX_LENGTH;
            var endOfNameMarkOffset = nameOffset + nameLength;
            var valueOffset = endOfNameMarkOffset + END_OF_NAME_MARK_LENGTH;
            var endOfValueMarkOffset = valueOffset + valueLength;

            var indexMemoryBlock = memoryBlock.Slice(INDEX_OFFSET, INDEX_LENGTH);
            var nameMemoryBlock = memoryBlock.Slice(nameOffset, nameLength);
            var endOfNameMarkMemoryBlock = memoryBlock.Slice(endOfNameMarkOffset, END_OF_NAME_MARK_LENGTH);
            var valueMemoryBlock = memoryBlock.Slice(valueOffset, valueLength);
            var endOfValueMarkMemoryBlock = memoryBlock.Slice(endOfValueMarkOffset, END_OF_VALUE_MARK_LENGTH);

            endOfNameMarkMemoryBlock.Span[0] = endOfValueMarkMemoryBlock.Span[0] = 0;

            // The wire counts the occurrences of a header name from one, while the API counts from zero
            // like every other index a caller meets. Off by one here rewrites the neighbouring header.
            BinaryPrimitives.WriteUInt32BigEndian(indexMemoryBlock.Span, responsePacket.Index + 1);
            Encoding.Latin1.GetBytes(responsePacket.Name, nameMemoryBlock.Span);
            Encoding.Latin1.GetBytes(responsePacket.Value, valueMemoryBlock.Span);

            _responseFrame = new(ResponseCode.ChangeOrDeleteHeader, memoryOwner, ..blockLength);

            return true;
        }
        catch
        {
            memoryOwner.Dispose();

            throw;
        }
    }

    protected internal override bool VisitChangeSenderResponsePacket(ChangeSenderResponsePacket responsePacket)
    {
        var senderLength = Encoding.Latin1.GetByteCount(responsePacket.Sender);
        const int END_OF_SENDER_MARK_LENGTH = 1;

        // The MTA reads the arguments only if bytes remain after the sender, so "none" is an absent
        // field rather than an empty one. Writing a lone terminator instead would hand it an empty
        // argument string, which is not the same thing.
        var hasArguments = !string.IsNullOrEmpty(responsePacket.ESmtpArguments);
        var argumentsLength = hasArguments ? Encoding.Latin1.GetByteCount(responsePacket.ESmtpArguments!) : 0;
        var endOfArgumentsMarkLength = hasArguments ? 1 : 0;
        var blockLength = senderLength + END_OF_SENDER_MARK_LENGTH + argumentsLength + endOfArgumentsMarkLength;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(blockLength);

        try
        {
            var memoryBlock = memoryOwner.Memory[..blockLength];

            const int SENDER_OFFSET = 0;
            var endOfSenderMarkOffset = SENDER_OFFSET + senderLength;
            var argumentsOffset = endOfSenderMarkOffset + END_OF_SENDER_MARK_LENGTH;

            var senderMemoryBlock = memoryBlock.Slice(SENDER_OFFSET, senderLength);
            var endOfSenderMarkMemoryBlock = memoryBlock.Slice(endOfSenderMarkOffset, END_OF_SENDER_MARK_LENGTH);

            endOfSenderMarkMemoryBlock.Span[0] = 0;

            Encoding.Latin1.GetBytes(responsePacket.Sender, senderMemoryBlock.Span);

            if (hasArguments)
            {
                var argumentsMemoryBlock = memoryBlock.Slice(argumentsOffset, argumentsLength);
                var endOfArgumentsMarkMemoryBlock = memoryBlock.Slice(argumentsOffset + argumentsLength, endOfArgumentsMarkLength);

                endOfArgumentsMarkMemoryBlock.Span[0] = 0;

                Encoding.Latin1.GetBytes(responsePacket.ESmtpArguments!, argumentsMemoryBlock.Span);
            }

            _responseFrame = new(ResponseCode.ChangeSender, memoryOwner, ..blockLength);

            return true;
        }
        catch
        {
            memoryOwner.Dispose();

            throw;
        }
    }

    protected internal override bool VisitContinueProcessingResponsePacket(ContinueProcessingResponsePacket responsePacket)
    {
        _responseFrame = new(ResponseCode.ContinueProcessing);

        return true;
    }

    protected internal override bool VisitDeferMessageResponsePacket(DeferMessageResponsePacket responsePacket)
    {
        _responseFrame = new(ResponseCode.DeferMessage);

        return true;
    }

    protected internal override bool VisitDeleteRecipientResponsePacket(DeleteRecipientResponsePacket responsePacket)
    {
        var recipientLength = Encoding.Latin1.GetByteCount(responsePacket.Recipient);
        const int END_OF_RECIPIENT_MARK_LENGTH = 1;
        var blockLength = recipientLength + END_OF_RECIPIENT_MARK_LENGTH;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(blockLength);

        try
        {
            var memoryBlock = memoryOwner.Memory[..blockLength];

            const int RECIPIENT_OFFSET = 0;
            var endOfRecipientMarkOffset = RECIPIENT_OFFSET + recipientLength;

            var recipientMemoryBlock = memoryBlock.Slice(RECIPIENT_OFFSET, recipientLength);
            var endOfRecipientMarkMemoryBlock = memoryBlock.Slice(endOfRecipientMarkOffset, END_OF_RECIPIENT_MARK_LENGTH);

            endOfRecipientMarkMemoryBlock.Span[0] = 0;

            Encoding.Latin1.GetBytes(responsePacket.Recipient, recipientMemoryBlock.Span);

            _responseFrame = new(ResponseCode.DeleteRecipient, memoryOwner, ..blockLength);

            return true;
        }
        catch
        {
            memoryOwner.Dispose();

            throw;
        }
    }

    protected internal override bool VisitDiscardMessageResponsePacket(DiscardMessageResponsePacket responsePacket)
    {
        _responseFrame = new(ResponseCode.DiscardMessage);

        return true;
    }

    protected internal override bool VisitInsertHeaderResponsePacket(InsertHeaderResponsePacket responsePacket)
    {
        const int INDEX_LENGTH = sizeof(uint);
        var nameLength = Encoding.Latin1.GetByteCount(responsePacket.Name);
        const int END_OF_NAME_MARK_LENGTH = 1;
        var valueLength = Encoding.Latin1.GetByteCount(responsePacket.Value);
        const int END_OF_VALUE_MARK_LENGTH = 1;
        var blockLength = INDEX_LENGTH + nameLength + END_OF_NAME_MARK_LENGTH + valueLength + END_OF_VALUE_MARK_LENGTH;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(blockLength);

        try
        {
            var memoryBlock = memoryOwner.Memory[..blockLength];

            const int INDEX_OFFSET = 0;
            var nameOffset = INDEX_OFFSET + INDEX_LENGTH;
            var endOfNameMarkOffset = nameOffset + nameLength;
            var valueOffset = endOfNameMarkOffset + END_OF_NAME_MARK_LENGTH;
            var endOfValueMarkOffset = valueOffset + valueLength;

            var indexMemoryBlock = memoryBlock.Slice(INDEX_OFFSET, INDEX_LENGTH);
            var nameMemoryBlock = memoryBlock.Slice(nameOffset, nameLength);
            var endOfNameMarkMemoryBlock = memoryBlock.Slice(endOfNameMarkOffset, END_OF_NAME_MARK_LENGTH);
            var valueMemoryBlock = memoryBlock.Slice(valueOffset, valueLength);
            var endOfValueMarkMemoryBlock = memoryBlock.Slice(endOfValueMarkOffset, END_OF_VALUE_MARK_LENGTH);

            endOfNameMarkMemoryBlock.Span[0] = endOfValueMarkMemoryBlock.Span[0] = 0;

            // Written through, unlike a header change. Insertion counts from zero on the wire as well as
            // in the API - Postfix says so where it reads the field, "With Sendmail 8, index 0 means the
            // top-most header", and adds the one itself before handing the position on. A change counts
            // occurrences from one and does need the translation, which is what this used to copy, and
            // the cost of the copy was a header landing under its intended neighbour rather than over it.
            BinaryPrimitives.WriteUInt32BigEndian(indexMemoryBlock.Span, responsePacket.Index);
            Encoding.Latin1.GetBytes(responsePacket.Name, nameMemoryBlock.Span);
            Encoding.Latin1.GetBytes(responsePacket.Value, valueMemoryBlock.Span);

            _responseFrame = new(ResponseCode.InsertHeader, memoryOwner, ..blockLength);

            return true;
        }
        catch
        {
            memoryOwner.Dispose();

            throw;
        }
    }

    protected internal override bool VisitOptionNegotiationResponsePacket(OptionNegotiationResponsePacket responsePacket)
    {
        const int VERSION_LENGTH = sizeof(uint);
        const int ACTIONS_LENGTH = sizeof(uint);
        const int UNDESIRED_CONTENT_LENGTH = sizeof(uint);
        const int HEADER_LENGTH = VERSION_LENGTH + ACTIONS_LENGTH + UNDESIRED_CONTENT_LENGTH;
        const int STAGE_CODE_LENGTH = sizeof(uint);
        const int END_OF_NAMES_MARK_LENGTH = 1;

        // Materialised once and in ascending stage order. A dictionary has no defined enumeration order,
        // and this frame is asserted on byte for byte.
        var macroLists = ToMacroLists(responsePacket.MilterRequestedMacros);

        var blockLength = HEADER_LENGTH;

        foreach (var (_, macroNames) in macroLists)
            blockLength += STAGE_CODE_LENGTH + Encoding.Latin1.GetByteCount(macroNames) + END_OF_NAMES_MARK_LENGTH;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(blockLength);

        try
        {
            var memoryBlock = memoryOwner.Memory[..blockLength];

            const int VERSION_OFFSET = 0;
            const int ACTIONS_OFFSET = VERSION_OFFSET + VERSION_LENGTH;
            const int UNDESIRED_CONTENT_OFFSET = ACTIONS_OFFSET + ACTIONS_LENGTH;

            var protocolVersionMemoryBlock = memoryBlock.Slice(VERSION_OFFSET, VERSION_LENGTH);
            var requestedActionsMemoryBlock = memoryBlock.Slice(ACTIONS_OFFSET, ACTIONS_LENGTH);
            var undesiredProtocolContentMemoryBlock = memoryBlock.Slice(UNDESIRED_CONTENT_OFFSET, UNDESIRED_CONTENT_LENGTH);

            BinaryPrimitives.WriteUInt32BigEndian(protocolVersionMemoryBlock.Span, (uint)responsePacket.MilterProtocolVersion.Major);
            BinaryPrimitives.WriteUInt32BigEndian(requestedActionsMemoryBlock.Span, (uint)responsePacket.MilterRequiredActions);
            BinaryPrimitives.WriteUInt32BigEndian(undesiredProtocolContentMemoryBlock.Span, (uint)responsePacket.MilterUndesiredProtocolContent);

            // With no lists to append this loop does not run, and the frame is the same twelve bytes it
            // has always been - which is what keeps every version 2 negotiation byte-identical.
            var offset = HEADER_LENGTH;

            foreach (var (stageCode, macroNames) in macroLists)
            {
                var namesLength = Encoding.Latin1.GetByteCount(macroNames);

                var stageCodeMemoryBlock = memoryBlock.Slice(offset, STAGE_CODE_LENGTH);
                var macroNamesMemoryBlock = memoryBlock.Slice(offset + STAGE_CODE_LENGTH, namesLength);
                var endOfNamesMarkMemoryBlock = memoryBlock.Slice(offset + STAGE_CODE_LENGTH + namesLength, END_OF_NAMES_MARK_LENGTH);

                endOfNamesMarkMemoryBlock.Span[0] = 0;

                BinaryPrimitives.WriteUInt32BigEndian(stageCodeMemoryBlock.Span, stageCode);
                Encoding.Latin1.GetBytes(macroNames, macroNamesMemoryBlock.Span);

                offset += STAGE_CODE_LENGTH + namesLength + END_OF_NAMES_MARK_LENGTH;
            }

            _responseFrame = new(ResponseCode.OptionNegotiation, memoryOwner, ..blockLength);

            return true;
        }
        catch
        {
            memoryOwner.Dispose();

            throw;
        }
    }

    protected internal override bool VisitProgressResponsePacket(ProgressResponsePacket responsePacket)
    {
        _responseFrame = new(ResponseCode.Progress);

        return true;
    }

    protected internal override bool VisitQuarantineMessageResponsePacket(QuarantineMessageResponsePacket responsePacket)
    {
        var reasonLength = Encoding.Latin1.GetByteCount(responsePacket.Reason);
        const int END_OF_REASON_MARK_LENGTH = 1;
        var blockLength = reasonLength + END_OF_REASON_MARK_LENGTH;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(blockLength);

        try
        {
            var memoryBlock = memoryOwner.Memory[..blockLength];

            const int REASON_OFFSET = 0;
            var endOfReasonMarkOffset = REASON_OFFSET + reasonLength;

            var reasonMemoryBlock = memoryBlock.Slice(REASON_OFFSET, reasonLength);
            var endOfReasonMarkMemoryBlock = memoryBlock.Slice(endOfReasonMarkOffset, END_OF_REASON_MARK_LENGTH);

            endOfReasonMarkMemoryBlock.Span[0] = 0;

            Encoding.Latin1.GetBytes(responsePacket.Reason, reasonMemoryBlock.Span);

            _responseFrame = new(ResponseCode.QuarantineMessage, memoryOwner, ..blockLength);

            return true;
        }
        catch
        {
            memoryOwner.Dispose();

            throw;
        }
    }

    protected internal override bool VisitRejectMessageResponsePacket(RejectMessageResponsePacket responsePacket)
    {
        _responseFrame = new(ResponseCode.RejectMessage);

        return true;
    }

    protected internal override bool VisitReplaceBodyResponsePacket(ReplaceBodyResponsePacket responsePacket)
    {
        _responseFrame = new(ResponseCode.ReplaceBody, responsePacket.Memory, responsePacket.BodyRange);

        return true;
    }

    protected internal override bool VisitReplyWithCodeResponsePacket(ReplyWithCodeResponsePacket responsePacket)
    {
        var codeLength = Encoding.Latin1.GetByteCount(responsePacket.SmtpCode);
        const int END_OF_CODE_MARK_LENGTH = 1;
        var textLength = GetByteCountWithDoubledPercentSigns(responsePacket.Text);
        const int END_OF_TEXT_MARK_LENGTH = 1;
        var blockLength = codeLength + END_OF_CODE_MARK_LENGTH + textLength + END_OF_TEXT_MARK_LENGTH;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(blockLength);

        try
        {
            var memoryBlock = memoryOwner.Memory[..blockLength];

            const int CODE_OFFSET = 0;
            var endOfCodeMarkOffset = CODE_OFFSET + codeLength;
            var textOffset = endOfCodeMarkOffset + END_OF_CODE_MARK_LENGTH;
            var endOfTextMarkOffset = textOffset + textLength;

            var codeMemoryBlock = memoryBlock.Slice(CODE_OFFSET, codeLength);
            var endOfCodeMarkMemoryBlock = memoryBlock.Slice(endOfCodeMarkOffset, END_OF_CODE_MARK_LENGTH);
            var textMemoryBlock = memoryBlock.Slice(textOffset, textLength);
            var endOfTextMarkMemoryBlock = memoryBlock.Slice(endOfTextMarkOffset, END_OF_TEXT_MARK_LENGTH);

            endOfCodeMarkMemoryBlock.Span[0] = (byte)' ';
            endOfTextMarkMemoryBlock.Span[0] = 0;

            Encoding.Latin1.GetBytes(responsePacket.SmtpCode, codeMemoryBlock.Span);
            WriteWithDoubledPercentSigns(responsePacket.Text, textMemoryBlock.Span);

            _responseFrame = new(ResponseCode.ReplyWithCode, memoryOwner, ..blockLength);

            return true;
        }
        catch
        {
            memoryOwner.Dispose();

            throw;
        }
    }

    protected internal override bool VisitSkipRemainingEventsResponsePacket(SkipRemainingEventsResponsePacket responsePacket)
    {
        _responseFrame = new(ResponseCode.Skip);

        return true;
    }

    // Postfix runs the reply text through a printf-style formatter and un-doubles percent signs on the
    // way (milter8.c), so a literal one has to reach it doubled or it swallows the character along with
    // whatever follows it.
    private static int GetByteCountWithDoubledPercentSigns(string text)
    {
        return Encoding.Latin1.GetByteCount(text) + text.AsSpan().Count('%');
    }

    private static void WriteWithDoubledPercentSigns(string text, Span<byte> destination)
    {
        var remaining = text.AsSpan();

        while (true)
        {
            var percentIndex = remaining.IndexOf('%');

            if (percentIndex < 0)
            {
                Encoding.Latin1.GetBytes(remaining, destination);

                return;
            }

            var writtenLength = Encoding.Latin1.GetBytes(remaining[..(percentIndex + 1)], destination);

            destination[writtenLength] = (byte)'%';
            destination = destination[(writtenLength + 1)..];
            remaining = remaining[(percentIndex + 1)..];
        }
    }

    // One entry per stage, ascending by the code the wire uses, with the names already joined. The
    // joining and the list allocate, and this runs once per connection - not worth a pooled rewrite, but
    // worth saying so rather than leaving the reader to wonder.
    private static IReadOnlyList<(uint StageCode, string MacroNames)> ToMacroLists(MacroRequirements? macroRequirements)
    {
        if (macroRequirements is null or { IsEmpty: true })
            return [];

        return macroRequirements.ByStage
            .Select(entry => (StageCode: GetMacroStageCode(entry.Key), MacroNames: string.Join(" ", entry.Value)))
            .OrderBy(entry => entry.StageCode)
            .ToArray();
    }

    // The protocol's macro class for a stage (SMFIM_), which is deliberately not ProtocolStage's own
    // number: conversation order runs Header before EndOfHeaders, the macro classes run the other way
    // round. Header and Body have no class at all - MacroRequirements refuses to name either, so a stage
    // arriving here without one is a bug in that guard rather than something a caller did.
    private static uint GetMacroStageCode(ProtocolStage stage)
    {
        return stage switch
        {
            ProtocolStage.Connect => 0,
            ProtocolStage.ClientHello => 1,
            ProtocolStage.Sender => 2,
            ProtocolStage.Recipient => 3,
            ProtocolStage.MessageData => 4,
            ProtocolStage.EndOfBody => 5,
            ProtocolStage.EndOfHeaders => 6,
            _ => throw new ArgumentException($"The protocol defines no macro class for {stage}.", nameof(stage)),
        };
    }
}
