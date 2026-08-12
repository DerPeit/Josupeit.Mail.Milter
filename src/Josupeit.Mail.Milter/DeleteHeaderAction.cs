using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

public sealed record DeleteHeaderAction : ModificationAction
{
    internal DeleteHeaderAction(string name, int index)
    {
        Name = name;
        Index = index;
    }

    public string Name { get; }

    public int Index { get; }

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
            Value = string.Empty,
        };
    }
}