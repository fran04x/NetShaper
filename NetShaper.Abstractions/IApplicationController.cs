// NetShaper.Abstractions/IApplicationController.cs
using System.Threading;
using System.Threading.Tasks;

namespace NetShaper.Abstractions
{
    public interface IApplicationController
    {
        Task<int> RunAsync(CancellationToken ct);
    }
}
