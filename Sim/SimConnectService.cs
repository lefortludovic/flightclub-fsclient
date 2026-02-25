using System.Windows.Interop;
using FlightClub.FsClient.Models;
using Microsoft.FlightSimulator.SimConnect;

namespace FlightClub.FsClient.Sim;

public class SimConnectService : IDisposable
{
    private const int WM_USER_SIMCONNECT = 0x0402;

    private SimConnect? _simConnect;
    private HwndSource? _hwndSource;
    private bool _disposed;

    public event Action<AircraftData>? OnAircraftDataUpdated;
    public event Action<string>? OnStatusChanged;
    public event Action<Exception>? OnError;

    public bool IsConnected => _simConnect != null;

    public void Connect(IntPtr windowHandle)
    {
        try
        {
            _simConnect = new SimConnect("FlightClub.FsClient", windowHandle, WM_USER_SIMCONNECT, null, 0);

            _simConnect.OnRecvOpen += SimConnect_OnRecvOpen;
            _simConnect.OnRecvQuit += SimConnect_OnRecvQuit;
            _simConnect.OnRecvSimobjectData += SimConnect_OnRecvSimobjectData;
            _simConnect.OnRecvSimobjectDataBytype += SimConnect_OnRecvSimobjectDataBytype;
            _simConnect.OnRecvException += SimConnect_OnRecvException;

            RegisterDataDefinitions();

            // Hook into WPF message pump
            _hwndSource = HwndSource.FromHwnd(windowHandle);
            _hwndSource?.AddHook(WndProc);

            OnStatusChanged?.Invoke("Connected to MSFS");
        }
        catch (Exception ex)
        {
            // Clean up partially-created connection to avoid getting stuck
            // with IsConnected returning true on a broken connection
            if (_simConnect != null)
            {
                try { _simConnect.Dispose(); } catch { }
                _simConnect = null;
            }
            if (_hwndSource != null)
            {
                try { _hwndSource.RemoveHook(WndProc); } catch { }
                _hwndSource = null;
            }

            OnError?.Invoke(ex);
            OnStatusChanged?.Invoke("Failed to connect to MSFS");
        }
    }

    public void Disconnect()
    {
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        if (_simConnect != null)
        {
            _simConnect.Dispose();
            _simConnect = null;
        }

        OnStatusChanged?.Invoke("Disconnected from MSFS");
    }

    public void RequestData()
    {
        _simConnect?.RequestDataOnSimObjectType(
            DataRequests.AircraftPositionRequest,
            DataDefinitions.AircraftPosition,
            0,
            SIMCONNECT_SIMOBJECT_TYPE.USER);
    }

    public void StartPeriodicDataRequest()
    {
        _simConnect?.RequestDataOnSimObject(
            DataRequests.AircraftPositionRequest,
            DataDefinitions.AircraftPosition,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
            0, 0, 0);
    }

    private void RegisterDataDefinitions()
    {
        if (_simConnect == null) return;

        _simConnect.AddToDataDefinition(DataDefinitions.AircraftPosition,
            "PLANE LATITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(DataDefinitions.AircraftPosition,
            "PLANE LONGITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(DataDefinitions.AircraftPosition,
            "PLANE ALTITUDE", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(DataDefinitions.AircraftPosition,
            "GROUND VELOCITY", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(DataDefinitions.AircraftPosition,
            "PLANE HEADING DEGREES TRUE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        _simConnect.AddToDataDefinition(DataDefinitions.AircraftPosition,
            "SIM ON GROUND", "bool", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        _simConnect.RegisterDataDefineStruct<SimConnectAircraftPosition>(DataDefinitions.AircraftPosition);
    }

    private void SimConnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
    {
        OnStatusChanged?.Invoke("Connected to MSFS");
        StartPeriodicDataRequest();
    }

    private void SimConnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        Disconnect();
    }

    private void SimConnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID == (uint)DataRequests.AircraftPositionRequest)
        {
            HandleAircraftData(data.dwData[0]);
        }
    }

    private void SimConnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
    {
        if (data.dwRequestID == (uint)DataRequests.AircraftPositionRequest)
        {
            HandleAircraftData(data.dwData[0]);
        }
    }

    private void HandleAircraftData(object rawData)
    {
        var position = (SimConnectAircraftPosition)rawData;
        var aircraftData = new AircraftData
        {
            Latitude = position.Latitude,
            Longitude = position.Longitude,
            Altitude = position.Altitude,
            GroundSpeed = position.GroundVelocity,
            Heading = position.Heading,
            OnGround = position.SimOnGround > 0.5,
            Timestamp = DateTime.UtcNow
        };

        OnAircraftDataUpdated?.Invoke(aircraftData);
    }

    private void SimConnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
    {
        OnError?.Invoke(new Exception($"SimConnect exception: {data.dwException}"));
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_USER_SIMCONNECT)
        {
            _simConnect?.ReceiveMessage();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                Disconnect();
            }
            catch
            {
                // SimConnect native DLL may not be loadable during shutdown
            }
            _disposed = true;
        }
    }
}
