namespace GuacamoleSharp.Options
{
    internal sealed class GuacdOptions
    {
        public const string Name = "Guacd";

        public string Hostname { get; set; } = "localhost";

        public int Port { get; set; } = 4822;
    }
}