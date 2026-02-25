using System.Runtime.InteropServices;

namespace FlightClub.FsClient.Sim;

public enum DataDefinitions
{
    AircraftPosition
}

public enum DataRequests
{
    AircraftPositionRequest
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct SimConnectAircraftPosition
{
    public double Latitude;
    public double Longitude;
    public double Altitude;
    public double GroundVelocity;
    public double Heading;
    public double SimOnGround;
}
