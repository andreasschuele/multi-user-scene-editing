using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MUSE.Server.Middleware
{
    /// <summary>
    /// The WebSocketMiddleware class is responsible for hosting the MUSE server, accepting WebSockets connections and delegating them to the MUSE server. 
    /// </summary>
    public class WebSocketMiddleware
    {
        /// <summary>
        /// The next function in the processing chain to process the HTTP request.
        /// </summary>
        private readonly RequestDelegate next;

        /// <summary>
        /// A logger.
        /// </summary>
        private ILogger logger;

        /// <summary>
        /// The MUSE Server object.
        /// </summary>
        private Server server;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="next">A function that can process a HTTP request.</param>
        /// <param name="loggerFactory"></param>
        public WebSocketMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            this.next = next;
            this.logger = loggerFactory.CreateLogger<WebSocketMiddleware>();
            this.server = new Server();
        }

        /// <summary>
        /// This method is invoked on every request along the HTTP processing chain.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task Invoke(HttpContext context)
        {
            // Proceed only if this is a WebSocket request. 

            if (context.Request.Path != "/ws" || !context.WebSockets.IsWebSocketRequest)
            {
                await next.Invoke(context);
                return;
            }

            CancellationToken ct = context.RequestAborted;
            WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();

            logger.LogInformation("Client connected.");

            // Create a new MUSE client wrapper class for this WebSocket object.

            Client client = new Client(Guid.NewGuid().ToString(), webSocket, ct, server);

            server.AddClient(client);
            
            // Loop until a connection cancelation has been requested.

            while (true)
            {
                if (ct.IsCancellationRequested || client.IsDisconnectionRequested)
                {
                    break;
                }

                Thread.Sleep(10);
            }

            server.RemoveClient(client);

            // Close the socket if not already aborted.

            if (webSocket.State != WebSocketState.Aborted)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
            }

            webSocket.Dispose();

            logger.LogInformation("Client disconnected.");
        }
    }
}
