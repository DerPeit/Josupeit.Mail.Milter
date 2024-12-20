using System.Threading;
using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Session;

public interface ISession
{
    ValueTask Run(CancellationToken cancellationToken);
}