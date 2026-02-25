using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FlightClub.FsClient.Helpers;
using FlightClub.FsClient.Models;
using FlightClub.FsClient.Services;
using FlightClub.FsClient.Sim;

namespace FlightClub.FsClient.ViewModels;

public partial class MainViewModel : ObservableObject
{
#if DEBUG
    private const string BaseUrl = "https://app-staging-6e64.up.railway.app";
#else
    private const string BaseUrl = "https://www.flightclub.be";
#endif
    private const int PairingPollIntervalMs = 2000;
    private const int SimRetryIntervalMs = 5000;
    private const int SessionRetryIntervalMs = 5000;

    private readonly SimConnectService _simConnectService;
    private SignalRStreamingService? _streamingService;
    private SessionService? _sessionService;
    private SessionInfo? _currentSession;
    private CancellationTokenSource? _pairingCts;
    private CancellationTokenSource? _autoConnectCts;

    [ObservableProperty]
    private bool _isSimConnected;

    [ObservableProperty]
    private bool _isHubConnected;

    [ObservableProperty]
    private bool _isPaired;

    [ObservableProperty]
    private string _simStatus = "Waiting for MSFS...";

    [ObservableProperty]
    private string _hubStatus = "Not connected";

    [ObservableProperty]
    private string _pairingStatus = "No active session";

    [ObservableProperty]
    private BitmapImage? _qrCodeImage;

    [ObservableProperty]
    private string _sessionId = string.Empty;

    [ObservableProperty]
    private double _latitude;

    [ObservableProperty]
    private double _longitude;

    [ObservableProperty]
    private double _altitude;

    [ObservableProperty]
    private double _groundSpeed;

    [ObservableProperty]
    private double _heading;

    [ObservableProperty]
    private bool _onGround;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private bool _hasAircraftData;

    private IntPtr _windowHandle;
    private readonly Dispatcher _dispatcher;

    public MainViewModel()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _simConnectService = new SimConnectService();

        _simConnectService.OnAircraftDataUpdated += OnAircraftDataReceived;
        _simConnectService.OnStatusChanged += status => _dispatcher.Invoke(() =>
        {
            SimStatus = status;
            IsSimConnected = _simConnectService.IsConnected;
        });
        _simConnectService.OnError += ex => _dispatcher.Invoke(() =>
        {
            SimStatus = $"Error: {ex.Message}";
            IsSimConnected = false;
            HasAircraftData = false;
        });
    }

    /// <summary>
    /// Called from MainWindow once the window handle is available.
    /// Kicks off automatic connection to both MSFS and the API session.
    /// </summary>
    public void Initialize(IntPtr handle)
    {
        _windowHandle = handle;
        _autoConnectCts = new CancellationTokenSource();

        SimStatus = "Initializing...";

        _ = AutoStartSessionAsync(_autoConnectCts.Token);
        _ = AutoConnectSimAsync(_autoConnectCts.Token);
    }

    private async Task AutoStartSessionAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                HubStatus = "Connecting...";
                _sessionService = new SessionService(BaseUrl);
                _currentSession = await _sessionService.CreateSessionAsync();

                if (_currentSession == null)
                {
                    HubStatus = "Failed to create session";
                    await Task.Delay(SessionRetryIntervalMs, ct);
                    continue;
                }

                // Generate QR code for pairing
                var pairingUrl = $"{BaseUrl}/sim/pair/{_currentSession.SessionId}";
                _dispatcher.Invoke(() =>
                {
                    QrCodeImage = QrCodeHelper.GenerateQrCode(pairingUrl);
                    SessionId = _currentSession.SessionId;
                    PairingStatus = "Scan QR code to pair...";
                });

                // Connect to SignalR hub
                _streamingService = new SignalRStreamingService();
                _streamingService.OnStatusChanged += status => _dispatcher.Invoke(() =>
                {
                    HubStatus = status;
                    IsHubConnected = _streamingService.IsConnected;
                });

                await _streamingService.ConnectAsync(
                    _currentSession.HubUrl,
                    _currentSession.SessionId,
                    _currentSession.Token);

                _dispatcher.Invoke(() =>
                {
                    IsHubConnected = _streamingService.IsConnected;
                    HubStatus = "Connected";
                });

                // Start polling for pairing status
                _pairingCts?.Cancel();
                _pairingCts = new CancellationTokenSource();
                _ = PollPairingStatusAsync(_pairingCts.Token);

                // Session started successfully, exit the retry loop
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _dispatcher.Invoke(() =>
                {
                    HubStatus = $"Retrying... ({ex.Message})";
                    IsHubConnected = false;
                });
                await Task.Delay(SessionRetryIntervalMs, ct);
            }
        }
    }

    private async Task AutoConnectSimAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (!_simConnectService.IsConnected && _windowHandle != IntPtr.Zero)
                    {
                        _dispatcher.Invoke(() =>
                        {
                            SimStatus = "Looking for MSFS...";
                            _simConnectService.Connect(_windowHandle);
                            IsSimConnected = _simConnectService.IsConnected;
                            if (IsSimConnected)
                            {
                                IsStreaming = true;
                            }
                        });

                        if (_simConnectService.IsConnected)
                        {
                            // Connected — wait until disconnect, then resume retrying
                            while (_simConnectService.IsConnected && !ct.IsCancellationRequested)
                            {
                                await Task.Delay(SimRetryIntervalMs, ct);
                            }

                            // Disconnected — update state and fall through to retry
                            _dispatcher.Invoke(() =>
                            {
                                IsSimConnected = false;
                                IsStreaming = false;
                                HasAircraftData = false;
                                SimStatus = "MSFS disconnected, reconnecting...";
                            });
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Catch any exception (including JIT/DLL loading errors for SimConnect)
                    // so the retry loop keeps running instead of dying silently
                    _dispatcher.Invoke(() =>
                    {
                        SimStatus = $"Error: {ex.Message}";
                        IsSimConnected = false;
                    });
                }

                try
                {
                    await Task.Delay(SimRetryIntervalMs, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            // Last resort: catch anything that escaped inner handlers
            _dispatcher.Invoke(() =>
            {
                SimStatus = $"Fatal: {ex.GetType().Name}: {ex.Message}";
                IsSimConnected = false;
            });
        }
    }

    private async Task PollPairingStatusAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _currentSession != null && _sessionService != null)
        {
            try
            {
                var status = await _sessionService.GetSessionStatusAsync(_currentSession.SessionId);
                if (status?.IsPaired == true)
                {
                    _dispatcher.Invoke(() =>
                    {
                        IsPaired = true;
                        PairingStatus = "Paired! User connected.";
                    });
                    return;
                }

                if (status?.IsExpired == true)
                {
                    _dispatcher.Invoke(() =>
                    {
                        IsPaired = false;
                        PairingStatus = "Session expired";
                    });
                    return;
                }
            }
            catch
            {
                // Ignore polling errors, will retry
            }

            await Task.Delay(PairingPollIntervalMs, ct);
        }
    }

    private async void OnAircraftDataReceived(AircraftData data)
    {
        _dispatcher.Invoke(() =>
        {
            HasAircraftData = true;
            Latitude = data.Latitude;
            Longitude = data.Longitude;
            Altitude = data.Altitude;
            GroundSpeed = data.GroundSpeed;
            Heading = data.Heading;
            OnGround = data.OnGround;
        });

        // Stream to hub if connected
        if (_streamingService?.IsConnected == true)
        {
            try
            {
                await _streamingService.SendAircraftDataAsync(data);
            }
            catch
            {
                // Ignore send errors, hub will reconnect
            }
        }
    }

    public async Task CleanupAsync()
    {
        _autoConnectCts?.Cancel();
        _pairingCts?.Cancel();
        _simConnectService.Dispose();

        if (_streamingService != null)
        {
            await _streamingService.DisposeAsync();
        }
    }
}
