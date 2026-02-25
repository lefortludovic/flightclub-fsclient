using FlightClub.FsClient.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace FlightClub.FsClient.Services;

public class SignalRStreamingService : IAsyncDisposable
{
    private HubConnection? _hubConnection;

    public event Action<string>? OnStatusChanged;
    public event Action? OnDesktopConnected;
    public event Action? OnDesktopDisconnected;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(string hubUrl, string sessionId, string token)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{hubUrl}?sessionId={sessionId}&token={Uri.EscapeDataString(token)}&role=desktop")
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.Reconnecting += _ =>
        {
            OnStatusChanged?.Invoke("Reconnecting to hub...");
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += _ =>
        {
            OnStatusChanged?.Invoke("Reconnected to hub");
            return Task.CompletedTask;
        };

        _hubConnection.Closed += _ =>
        {
            OnStatusChanged?.Invoke("Hub connection closed");
            return Task.CompletedTask;
        };

        await _hubConnection.StartAsync();
        OnStatusChanged?.Invoke("Connected to hub");
    }

    public async Task SendAircraftDataAsync(AircraftData data)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("SendAircraftData", data);
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            OnStatusChanged?.Invoke("Disconnected from hub");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
