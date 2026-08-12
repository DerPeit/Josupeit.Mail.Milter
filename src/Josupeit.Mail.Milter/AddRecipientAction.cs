using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

public sealed record AddRecipientAction : ModificationAction
{
    internal AddRecipientAction(string recipient)
    {
        Recipient = recipient;
    }

    public string Recipient { get; }

    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return new AddRecipientResponsePacket
        {
            Recipient = Recipient,
        };
    }
}