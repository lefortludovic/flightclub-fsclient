namespace FlightClub.FsClient.Models;

public class AircraftData
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    public double GroundSpeed { get; set; }
    public double Heading { get; set; }
    public bool OnGround { get; set; }
    public DateTime Timestamp { get; set; }
}
