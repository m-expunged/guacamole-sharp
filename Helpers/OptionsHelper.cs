using GuacamoleSharp.Options;

namespace GuacamoleSharp.Helpers
{
    public static class OptionsHelper
    {
        public static ClientOptions Client { get; set; } = null!;

        public static GuacdOptions Guacd { get; set; } = null!;

        public static SocketOptions Socket { get; set; } = null!;
    }
}