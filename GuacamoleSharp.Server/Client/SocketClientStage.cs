namespace GuacamoleSharp.Server.Client
{
    internal enum SocketClientStage
    {
        UNDEFINED = 0,
        HANDSHAKE = 1,
        OPEN = 2,
        CLOSE = 3
    }
}
