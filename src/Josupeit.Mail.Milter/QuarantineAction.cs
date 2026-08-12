using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

public sealed record QuarantineAction : ModificationAction
{
    internal QuarantineAction(string reason)
    {
        Reason = reason;
    }

    public string Reason { get; }

    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return new QuarantineMessageResponsePacket
        {
            Reason = Reason,
        };
    }
}