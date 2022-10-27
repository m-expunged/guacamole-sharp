using GuacamoleSharp.Helpers;
using GuacamoleSharp.Logic.State;
using Serilog;
using System.Net.Sockets;
using System.Text;

namespace GuacamoleSharp.Logic
{
    public class Client
    {
        public static void Close(ConnectionState state)
        {
            try
            {
                lock (state.DisposeLock)
                {
                    if (state.Client.Socket != null && !state.Client.Closed)
                    {
                        Log.Information("[Connection {Id}] Closing client connection.", state.ConnectionId);

                        state.Client.Socket.Shutdown(SocketShutdown.Both);
                        state.Client.Socket.Close();
                    }

                    state.Client.Closed = true;
                }
            }
            catch (Exception ex)
            {
                Log.Error("[Connection {id}] Error while closing client connection: {ex}", state.ConnectionId, ex);
            }
        }

        public static void Send(ConnectionState state, string message, bool isWSF = true)
        {
            state.Client.SendDone.Reset();

            Log.Debug("[Connection {Id}] >>>G2W> {Message}", state.ConnectionId, message);

            byte[] data = isWSF ? WebSocketHelpers.WriteToFrame(message) : Encoding.UTF8.GetBytes(message);
            state.Client.Socket.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), state);

            state.Client.SendDone.WaitOne();
        }

        public static void Start(ConnectionState state)
        {
            Task.Run(() =>
            {
                state.Guacd.HandshakeDone.WaitOne();

                try
                {
                    while (!state.Client.Closed)
                    {
                        state.Client.ReceiveDone.Reset();

                        state.Client.Socket.BeginReceive(state.Client.Buffer, 0, state.Client.Buffer.Length, 0, new AsyncCallback(ReceiveCallback), state);

                        state.Client.ReceiveDone.WaitOne();
                    }
                }
                catch (ObjectDisposedException)
                {
                    Log.Warning("[Connection {Id}] Receive callback attempted to perform operation on disposed listener", state.ConnectionId);
                }
                catch (Exception ex)
                {
                    Log.Error("[Connection {Id}] Error while running socket listener thread: {ex}", state.ConnectionId, ex);
                    Guacd.Close(state);
                }
            });
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;

            if (state.Client.Closed)
            {
                state.Client.ReceiveDone.Set();
                return;
            }

            try
            {
                int receivedLength = state.Client.Socket.EndReceive(ar);

                if (state.Timeout) throw new Exception($"[Connection {state.ConnectionId}] Timeout.");

                if (receivedLength <= 0)
                {
                    state.Client.ReceiveDone.Set();
                    return;
                }

                try
                {
                    state.Client.OverflowBuffer.Append(WebSocketHelpers.ReadFromFrames(state.Client.Buffer[0..receivedLength], receivedLength));
                }
                catch (ArgumentOutOfRangeException)
                {
                    Log.Warning("[Connection {Id}] WebSocket frame could not be parsed. Skipping...", state.ConnectionId);

                    state.Client.ReceiveDone.Set();
                    return;
                }

                string response = state.Client.OverflowBuffer.ToString();
                (string? message, int delimiterIndex) = ProtocolHelper.ReadProtocolUntilLastDelimiter(response);

                if (message == null)
                {
                    state.Client.Socket.BeginReceive(state.Client.Buffer, 0, state.Client.Buffer.Length, 0, new AsyncCallback(ReceiveCallback), state);
                    return;
                }

                state.Client.OverflowBuffer.Remove(0, delimiterIndex);

                if (message.Contains("10.disconnect;")) throw new Exception($"[Connection {state.ConnectionId}] Disconnect.");

                Send(state, message);

                state.Client.ReceiveDone.Set();
            }
            catch (ObjectDisposedException)
            {
                Log.Warning("[Connection {Id}] Client socket tried to receive data from disposed connection.", state.ConnectionId);

                Guacd.Close(state);
                state.Client.ReceiveDone.Set();
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);

                Guacd.Close(state);
                state.Client.ReceiveDone.Set();
            }
        }

        private static void SendCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;

            if (state.Client.Closed)
            {
                state.Client.SendDone.Set();
                return;
            }

            try
            {
                state.Client.Socket.EndSend(ar);
            }
            catch (ObjectDisposedException)
            {
                Log.Warning("[Connection {Id}] Client socket tried to send data to disposed connection.", state.ConnectionId);

                Close(state);
                state.Client.SendDone.Set();
                return;
            }

            state.LastActivity = DateTimeOffset.Now;
            state.Client.SendDone.Set();
        }
    }
}