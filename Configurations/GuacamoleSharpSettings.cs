namespace GuacamoleSharp.Configurations
{
    public class GuacamoleSharpSettings
    {
        public ClientSettings Client { get; set; } = new();

        public GuacdSettings Guacd { get; set; } = new();

        public string Password { get; set; } = null!;

        public ServerSettings Server { get; set; } = new();
    }
}