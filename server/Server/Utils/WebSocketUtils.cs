using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MUSE.Server.Utils
{
    /// <summary>
    /// The WebSocketUtils class contains helper methods to handle sending and receiving of strings via WebSocket object.
    /// </summary>
    public class WebSocketUtils
    {
        /// <summary>
        /// Sends a string message asynchronously to a WebSocket.
        /// </summary>
        /// <param name="socket">A WebSocket that should receive the string message.</param>
        /// <param name="message">A string message.</param>
        /// <param name="ct">A CancellationToken object.</param>
        /// <returns></returns>
        public static Task SendStringAsync(WebSocket socket, string message, CancellationToken ct = default(CancellationToken))
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            ArraySegment<byte> segment = new ArraySegment<byte>(buffer);

            return socket.SendAsync(segment, WebSocketMessageType.Text, true, ct);
        }

        /// <summary>
        /// Receives a string message asynchronously from a WebSocket.
        /// </summary>
        /// <param name="socket">A WebSocket from which a message is to be received.</param>
        /// <param name="ct">A CancellationToken object.</param>
        /// <returns></returns>
        public static async Task<string> ReceiveStringAsync(WebSocket socket, CancellationToken ct = default(CancellationToken))
        {
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[4096]);

            using (MemoryStream memoryStream = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    ct.ThrowIfCancellationRequested();

                    result = await socket.ReceiveAsync(buffer, ct);
                    memoryStream.Write(buffer.Array, buffer.Offset, result.Count);
                }
                while (!result.EndOfMessage);

                memoryStream.Seek(0, SeekOrigin.Begin);

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    return null;
                }

                using (StreamReader streamReader = new StreamReader(memoryStream, Encoding.UTF8))
                {
                    return await streamReader.ReadToEndAsync();
                }
            }
        }
    }
}
