using Serilog.Core;
using Serilog.Events;

namespace GuacamoleSharp
{
    internal sealed class EnvironmentLoggingLevelSwitch : LoggingLevelSwitch
    {
        public EnvironmentLoggingLevelSwitch(string env)
        {
            if (Enum.TryParse(Environment.ExpandEnvironmentVariables(env), true, out LogEventLevel level))
            {
                MinimumLevel = level;
            }
        }
    }
}
