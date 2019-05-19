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
    /// This implements the MUSE client.
    /// </summary>
    public partial class Client : ClientBase
    {        
        public class ResourceFile
        {
            public string FilePath;

            public byte[] Data;

            public string Hash;
        }

        /// <summary>
        /// The session to which this client belongs.
        /// </summary>
        public Session Session { get; set; }
        
        /// <summary>
        /// The username of this client.
        /// </summary>
        public string Name;

        /// <summary>
        /// The user color.
        /// </summary>
        public string Color;

        public List<ResourceFile> UserResources = new List<ResourceFile>();
        
        public Client(string socketId, WebSocket socket, CancellationToken cancellationToken, Server server) : base(socketId, socket, cancellationToken, server)
        {
            RegisterMessageHandlers();
        }

        /// <summary>
        /// Enqueues a message for dispatch.
        /// </summary>
        /// <param name="message"></param>
        public void Send(Message message)
        {
            string museResponseMessage = "MUSE:" + message.AsJson();

            Outbox.Enqueue(museResponseMessage);
        }
    }
}
