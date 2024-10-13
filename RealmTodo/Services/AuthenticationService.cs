using RealmTodo.Models;
namespace RealmTodo.Services;

public class AuthenticationService : IAuthenticationService
{
    public async Task<User?> Login(string username, string password, CouchbaseAppConfig config)
    {
        if (config == null)
        {
            throw new InvalidOperationException("Must call CouchbaseService.init prior to calling Login");
        }

        var httpsUrl = config.EndpointUrl.Replace("wss:", "https:");
        var checkUrl = httpsUrl.Replace("/tasks", "/");
        if (!await IsUrlReachable(checkUrl))
        {
            throw new InvalidOperationException("Unable to connect to the server. Please check your network connection.");
        }
        var httpClient = GetHttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"))}"); 
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
        var response = await httpClient.GetAsync(httpsUrl);
        return response.IsSuccessStatusCode ? new User(username, password) : null;
    }
    
    private static async Task<bool> IsUrlReachable(string urlString)
    {
        try
        {
            var httpClient = GetHttpClient();
            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, urlString));
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    private static HttpClient GetHttpClient()
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        return httpClient;
    }
}