namespace GuacamoleSharp.Options
{
    internal sealed class GuacdOptions
    {
        public const string Name = "Guacd";

        public string Hostname { get; set; } = "127.0.0.1";

        public int Port { get; set; } = 4822;
    }
}