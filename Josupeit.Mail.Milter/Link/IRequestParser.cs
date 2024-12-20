using System.Threading.Tasks;

namespace Josupeit.Mail.Milter.Link;

public interface IRequestParser
{
    void Start();

    ValueTask StopAsync();
}