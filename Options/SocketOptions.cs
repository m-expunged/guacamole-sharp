namespace GuacamoleSharp.Options
{
    public sealed class SocketOptions
    {
        public const string Name = "Socket";

        public int MaxInactivityAllowedInMin { get; set; } = 10;

        public string Password { get; set; } = null!;

        public int Port { get; set; } = 8080;
    }
}