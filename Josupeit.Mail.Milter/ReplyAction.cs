using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Josupeit.Mail.Milter.Protocol;

namespace Josupeit.Mail.Milter;

public sealed record ReplyAction : CommitAction
{
    internal ReplyAction(int code, string text)
    {
        Code = code;
        Text = text;
    }

    public int Code { get; }

    public string Text { get; }

    protected override IAsyncEnumerable<ResponsePacket> CreateResponsePackets(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return CreateResponsePacketsCore().ToAsyncEnumerable();
    }

    private IEnumerable<ResponsePacket> CreateResponsePacketsCore()
    {
        yield return new ReplyWithCodeResponsePacket
        {
            SmtpCode = Code.ToString(CultureInfo.InvariantCulture),
            Text = Text,
        };
    }
}