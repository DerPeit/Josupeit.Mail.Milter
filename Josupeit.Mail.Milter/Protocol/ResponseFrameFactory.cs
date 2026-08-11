using System;
using Josupeit.Mail.Milter.Link;
using System.Buffers.Binary;
using System.Buffers;
using System.Text;

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
        const int endOfNameMarkLength = 1;
        var valueLength = Encoding.Latin1.GetByteCount(responsePacket.Value);
        const int endOfValueMarkLength = 1;
        var blockLength = nameLength + endOfNameMarkLength + valueLength + endOfValueMarkLength;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(blockLength);

        try
        {
            var memoryBlock = memoryOwner.Memory[..blockLength];

            var nameMemoryBlock = memoryBlock[..][..nameLength];
            var endOfNameMarkMemoryBlock = memoryBlock[nameLength..][..1];
            var valueMemoryBlock = memoryBlock[nameLength..][endOfNameMarkLength..][..valueLength];
            var endOfValueMarkMemoryBlock = memoryBlock[nameLength..][endOfNameMarkLength..][valueLength..][..1];

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
        const int endOfRecipientMarkLength = 1;
        var blockLength = recipientLength + endOfRecipientMarkLength;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(blockLength);

        try
        {
            var memoryBlock = memoryOwner.Memory[..blockLength];

            var recipientMemoryBlock = memoryBlock[..recipientLength];
            var endOfRecipientMarkMemoryBlock = memoryBlock[recipientLength..][..endOfRecipientMarkLength];

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
        const int indexLength = sizeof(uint);
        var nameLength = Encoding.Latin1.GetByteCount(responsePacket.Name);
        const int endOfNameMarkLength = 1;
        var valueLength = Encoding.Latin1.GetByteCount(responsePacket.Value);
        const int endOfValueMarkLength = 1;
        var blockLength = indexLength + nameLength + endOfNameMarkLength + valueLength + endOfValueMarkLength;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(blockLength);

        try
        {
            var memoryBlock = memoryOwner.Memory[..blockLength];

            var indexMemoryBlock = memoryBlock[..][..indexLength];
            var nameMemoryBlock = memoryBlock[indexLength..][..nameLength];
            var endOfNameMarkMemoryBlock = memoryBlock[indexLength..][nameLength..][..endOfNameMarkLength];
            var valueMemoryBlock = memoryBlock[indexLength..][nameLength..][endOfNameMarkLength..][..valueLength];
            var endOfValueMarkMemoryBlock = memoryBlock[indexLength..][nameLength..][endOfNameMarkLength..][valueLength..][..endOfValueMarkLength];

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
        const int endOfRecipientMarkLength = 1;
        var blockLength = recipientLength + endOfRecipientMarkLength;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(blockLength);

        try
        {
            var memoryBlock = memoryOwner.Memory[..blockLength];

            var recipientMemoryBlock = memoryBlock[..recipientLength];
            var endOfRecipientMarkMemoryBlock = memoryBlock[recipientLength..][..endOfRecipientMarkLength];

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
        const int versionLength = sizeof(uint);
        const int actionsLength = sizeof(uint);
        const int undesiredContentLength = sizeof(uint);
        const int blockLength = versionLength + actionsLength + undesiredContentLength;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(blockLength);

        try
        {
            var memoryBlock = memoryOwner.Memory[..blockLength];

            var protocolVersionMemoryBlock = memoryBlock[..][..versionLength];
            var requestedActionsMemoryBlock = memoryBlock[versionLength..][..actionsLength];
            var undesiredProtocolContentMemoryBlock = memoryBlock[versionLength..][actionsLength..][..undesiredContentLength];

            BinaryPrimitives.WriteUInt32BigEndian(protocolVersionMemoryBlock.Span, (uint)responsePacket.MilterProtocolVersion.Major);
            BinaryPrimitives.WriteUInt32BigEndian(requestedActionsMemoryBlock.Span, (uint)responsePacket.MilterRequiredActions);
            BinaryPrimitives.WriteUInt32BigEndian(undesiredProtocolContentMemoryBlock.Span, (uint)responsePacket.MilterUndesiredProtocolContent);

            _responseFrame = new(ResponseCode.OptionNegotiation, memoryOwner, ..blockLength);

            return true;
        }
        catch
        {
            memoryOwner.Dispose();

            throw;
        }
    }

    protected internal override bool VisitQuarantineMessageResponsePacket(QuarantineMessageResponsePacket responsePacket)
    {
        var reasonLength = Encoding.Latin1.GetByteCount(responsePacket.Reason);
        const int endOfReasonMarkLength = 1;
        var blockLength = reasonLength + endOfReasonMarkLength;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(blockLength);

        try
        {
            var memoryBlock = memoryOwner.Memory[..blockLength];

            var reasonMemoryBlock = memoryBlock[..reasonLength];
            var endOfReasonMarkMemoryBlock = memoryBlock[reasonLength..][..endOfReasonMarkLength];

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
        const int endOfCodeMarkLength = 1;
        var textLength = GetByteCountWithDoubledPercentSigns(responsePacket.Text);
        const int endOfTextMarkLength = 1;
        var blockLength = codeLength + endOfCodeMarkLength + textLength + endOfTextMarkLength;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(blockLength);

        try
        {
            var memoryBlock = memoryOwner.Memory[..blockLength];

            var codeMemoryBlock = memoryBlock[..][..codeLength];
            var endOfCodeMarkMemoryBlock = memoryBlock[codeLength..][..endOfCodeMarkLength];
            var textMemoryBlock = memoryBlock[codeLength..][endOfCodeMarkLength..][..textLength];
            var endOfTextMarkMemoryBlock = memoryBlock[codeLength..][endOfCodeMarkLength..][textLength..][..endOfTextMarkLength];

            endOfCodeMarkMemoryBlock.Span[0] = 32;
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