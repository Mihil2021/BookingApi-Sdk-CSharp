using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BookingApi.Core.Http.Extensions
{
    public static class HttpExtensions
    {
        public static async Task<T> ReadAsAsync<T>(this HttpContent content)
        {
            var rawJsonContent = await content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(rawJsonContent);
        }
    }
}
