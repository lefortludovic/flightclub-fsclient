using System.Net.Http;
using System.Net.Http.Json;
using FlightClub.FsClient.Models;

namespace FlightClub.FsClient.Services;

public class SessionService
{
    private readonly HttpClient _httpClient;

    public SessionService(string baseUrl)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task<SessionInfo?> CreateSessionAsync()
    {
        var response = await _httpClient.PostAsync("/api/sim/session", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionInfo>();
    }

    public async Task<SessionStatusResponse?> GetSessionStatusAsync(string sessionId)
    {
        var response = await _httpClient.GetAsync($"/api/sim/session/{sessionId}/status");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionStatusResponse>();
    }
}

public class SessionStatusResponse
{
    public string SessionId { get; set; } = string.Empty;
    public bool IsPaired { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
}
