using System.Net.Http.Json;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Mobile.Services
{
    public class CoinquiLindoApiClient(HttpClient httpClient)
    {
        public async Task<Guid?> CreateHouseAsync(string name)
        {
            var request = new CreateHouseRequest(name);

            var response = await httpClient.PostAsJsonAsync("/api/houses", request);

            if(response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<Guid>();
            }

            // TODO: Handle error response, log it, or throw an exception as needed

            return null;
        }

        public async Task<HouseResponse?> GetHouseAsync(Guid houseId)
        {
            try
            {
                return await httpClient.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}");
            }
            catch (HttpRequestException)
            {
                //TODO: Handler error response
                return null;
            }
        }

        public async Task<bool> AddTenantAsync(Guid houseId, string tenantName)
        {
            var request = new AddTenantRequest(tenantName);
            var response = await httpClient.PostAsJsonAsync($"/api/houses/{houseId}/tenants", request);

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> AddRoomAsync(Guid houseId, string name, List<string> cleaningTasks)
        {
            var request = new AddRoomRequest(name, cleaningTasks);
            var response = await httpClient.PostAsJsonAsync($"/api/houses/{houseId}/rooms", request);

            return response.IsSuccessStatusCode;
        }
    }
}
