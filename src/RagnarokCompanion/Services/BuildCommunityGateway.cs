using System.Threading;
using System.Threading.Tasks;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public interface IBuildCommunityGateway
{
 bool Configured {get;}
 Task<BuildCommunitySnapshot> FetchAsync(string classId,string stage,string purpose,CancellationToken ct);
 Task SubmitAsync(PlayerBuild build,CancellationToken ct);
}