namespace GuacamoleSharp.Configurations
{
    public class ServerSettings
    {
        public int InactivityAllowedInMin { get; set; } = 10;

        public int Port { get; set; } = 8080;
    }
}