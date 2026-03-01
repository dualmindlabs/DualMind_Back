using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DualMind.API.Infrastructure.Data
{
    public interface ISupabaseService
    {
        Task<List<T>> SelectAsync<T>(string table, string select = "*", string filter = null);
        Task<T> SelectSingleAsync<T>(string table, string select = "*", string filter = null);
        Task<T> InsertAsync<T>(string table, object data);
        Task<T> UpsertAsync<T>(string table, object data);
        Task<List<T>> UpdateAsync<T>(string table, object data, string filter);
        Task DeleteAsync(string table, string filter);
        Task<JObject> RpcAsync(string functionName, object parameters = null);
        Task<T> RpcAsync<T>(string functionName, object parameters = null);
    }
}
