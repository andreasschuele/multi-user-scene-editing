using MUSE.Server.Messages;
using MUSE.Server.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MUSE.Server
{
    /// <summary>
    /// An abstract implementation of the MUSE WebSocket client wrappter.
    /// </summary>
    public abstract class ClientBase
    {
        /// <summary>
        /// A function to process client events.
        /// </summary>
        /// <param name="client"></param>
        public delegate void ClientEvent(ClientBase client);

        /// <summary>
        /// Fires an event when the client is about to disconnect.
        /// </summary>
        public event ClientEvent OnClientDisconnect;

        /// <summary>
        /// Fires an event when the client is disconnected.
        /// </summary>
        public event ClientEvent OnClientDisconnected;

        /// <summary>
        /// An unique socket id.
        /// </summary>
        public string SocketId { get; }

        /// <summary>
        /// The WebSocket object of the client.
        /// </summary>
        public WebSocket Socket { get; }
        
        public bool IsDisconnectionRequested
        {
            get
            {
                return isDisconnectRequested;
            }
        }

        private CancellationToken cancellationToken;

        private Thread InboxThread;

        private Thread OutboxThread;

        public ConcurrentQueue<string> Inbox = new ConcurrentQueue<string>();

        public ConcurrentQueue<string> Outbox = new ConcurrentQueue<string>();

        private int outboxMessageIdCounter = 0;

        private bool isDisconnectRequested = false;

        protected Server server;

        public ClientBase(string socketId, WebSocket socket, CancellationToken cancellationToken, Server server)
        {
            this.SocketId = socketId;
            this.Socket = socket;
            this.cancellationToken = cancellationToken;
            this.server = server;
            this.InboxThread = new Thread(RunInbox);
            this.OutboxThread = new Thread(RunOutbox);
        }

        public void StartThreads()
        {
            InboxThread.Start();
            OutboxThread.Start();
        }

        private void RunInbox()
        {
            RunInboxAsync().Wait();
        }

        public void RunOutbox()
        {
            RunOutboxAsync().Wait();
        }

        public async Task RunInboxAsync()
        {
            while (true)
            {
                try
                {
                    if (Socket.State != WebSocketState.Open)
                        break;

                    var receivedString = await WebSocketUtils.ReceiveStringAsync(Socket, cancellationToken);
                   
                    if (string.IsNullOrEmpty(receivedString))
                        continue;

                    Console.WriteLine(receivedString);

                    if (receivedString.StartsWith("MUSE:"))
                    {
                        string museJsonMessage = receivedString.Substring(5);

                        HandleMessage(museJsonMessage);

                        continue;
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message, e.StackTrace);
                }
            }
        }

        public async Task RunOutboxAsync()
        {
            while (true)
            {
                try
                {
                    if (Socket.State != WebSocketState.Open)
                        break;

                    // Send messages from the outbox.

                    string message = null;

                    while (Outbox.TryDequeue(out message))
                    {
                        await WebSocketUtils.SendStringAsync(Socket, message, cancellationToken);
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message, e.StackTrace);
                }
            }
        }

        protected void SendStringAsMUSEFormat(string message)
        {
            Outbox.Enqueue("MUSE:" + message);
        }

        public void SendStringAsMUSEFormat(Message message)
        {
            message._smid = outboxMessageIdCounter++;

            SendStringAsMUSEFormat(message.AsJson());
        }

        public void OnClientRemoved()
        {
            OnClientDisconnected?.Invoke(this);
        }

        public void Disconnect()
        {
            OnClientDisconnect?.Invoke(this);

            isDisconnectRequested = true;
        }

        /// <summary>
        /// An abstract method that should be implemented to handle incoming messages.
        /// </summary>
        /// <param name="message"></param>
        protected abstract void HandleMessage(string message);
    }
}
