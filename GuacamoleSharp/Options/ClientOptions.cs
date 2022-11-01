namespace GuacamoleSharp.Options
{
    public sealed class ClientOptions
    {
        public const string Name = "Client";

        public Dictionary<string, Dictionary<string, string>> DefaultArguments { get; set; } = new();

        public Dictionary<string, List<string>> UnencryptedArguments { get; set; } = new();
    }
}