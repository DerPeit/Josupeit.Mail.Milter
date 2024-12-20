using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

public sealed record ChangeHeaderAction(string Name, string Value, int Index = 0) : ModificationAction
{
    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return new ChangeOrDeleteHeaderResponsePacket
        {
            Index = (uint)Index,
            Name = Name,
            Value = Value,
        };
    }
}