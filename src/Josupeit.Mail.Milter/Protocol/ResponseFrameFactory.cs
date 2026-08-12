// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Josupeit.Mail.Milter.Link;

namespace Josupeit.Mail.Milter.Protocol;

public class ResponseFrameFactory : IResponseFrameFactory
{
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

            _responseFrame = new(ResponseCode.AddRecipient, memoryOwner, ..blockLength);

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

    protected internal override bool VisitOptionNegotiationResponsePacket(OptionNegotiationResponsePacket responsePacket)
    {
        const int VERSION_LENGTH = sizeof(uint);
        const int ACTIONS_LENGTH = sizeof(uint);
        const int UNDESIRED_CONTENT_LENGTH = sizeof(uint);
        const int BLOCK_LENGTH = VERSION_LENGTH + ACTIONS_LENGTH + UNDESIRED_CONTENT_LENGTH;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(BLOCK_LENGTH);

        try
        {
            var memoryBlock = memoryOwner.Memory[..BLOCK_LENGTH];

            const int VERSION_OFFSET = 0;
            const int ACTIONS_OFFSET = VERSION_OFFSET + VERSION_LENGTH;
            const int UNDESIRED_CONTENT_OFFSET = ACTIONS_OFFSET + ACTIONS_LENGTH;

            var protocolVersionMemoryBlock = memoryBlock.Slice(VERSION_OFFSET, VERSION_LENGTH);
            var requestedActionsMemoryBlock = memoryBlock.Slice(ACTIONS_OFFSET, ACTIONS_LENGTH);
            var undesiredProtocolContentMemoryBlock = memoryBlock.Slice(UNDESIRED_CONTENT_OFFSET, UNDESIRED_CONTENT_LENGTH);

            BinaryPrimitives.WriteUInt32BigEndian(protocolVersionMemoryBlock.Span, (uint)responsePacket.MilterProtocolVersion.Major);
            BinaryPrimitives.WriteUInt32BigEndian(requestedActionsMemoryBlock.Span, (uint)responsePacket.MilterRequiredActions);
            BinaryPrimitives.WriteUInt32BigEndian(undesiredProtocolContentMemoryBlock.Span, (uint)responsePacket.MilterUndesiredProtocolContent);

            _responseFrame = new(ResponseCode.OptionNegotiation, memoryOwner, ..BLOCK_LENGTH);

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
}
