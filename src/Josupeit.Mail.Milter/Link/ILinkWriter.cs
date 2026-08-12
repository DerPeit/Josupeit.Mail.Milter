using System;
using System.Threading;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

public interface ILinkWriter
{
    ValueTask WriteUInt32(uint value, CancellationToken cancellationToken);

    ValueTask WriteResponseCode(ResponseCode responseCode, CancellationToken cancellationToken);

    ValueTask WriteData(ReadOnlySpan<byte> data, CancellationToken cancellationToken);
}