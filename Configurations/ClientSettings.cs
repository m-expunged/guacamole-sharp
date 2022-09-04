namespace GuacamoleSharp.Configurations
{
    public class ClientSettings
    {
        public Dictionary<string, Dictionary<string, string>> DefaultArguments { get; set; } = new();

        public Dictionary<string, List<string>> UnencryptedArguments { get; set; } = new();
    }
}