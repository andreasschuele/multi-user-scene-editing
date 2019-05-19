using MUSE.Server.Messages;
using MUSE.Server.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MUSE.Server
{
    public class Session
    {
        public delegate void SessiontEvent(Session session);
        public delegate void ClientEvent(Client client);

        public event SessiontEvent OnSessionStarted;
        public event SessiontEvent OnSessionEnded;
        public event ClientEvent OnClientJoined;
        public event ClientEvent OnClientLeft;

        /// <summary>
        /// The session name.
        /// </summary>
        public string Name;

        /// <summary>
        /// The session password.
        /// </summary>
        public string Password;

        /// <summary>
        /// A flag if project files have to be synchronized to session participants.
        /// </summary>
        public bool FileSync;

        /// <summary>
        /// Session node.
        /// </summary>
        public Node Node;

        /// <summary>
        /// Session participants.
        /// </summary>
        public ConcurrentDictionary<string, Client> Clients = new ConcurrentDictionary<string, Client>();

        /// <summary>
        /// Session owner.
        /// </summary>
        public Client Owner;

        public Dictionary<string, ResourceData> RESOURCE_DATA_CACHE = new Dictionary<string, ResourceData>();

        /// <summary>
        /// Session thread.
        /// </summary>
        private Thread sessionThread;

        public Session(string name)
        {
            this.Name = name;

            OnClientJoined += (client) =>
            {
                // Broadcast to notify who joined.

                Broadcast(new SessionUserJoined()
                {
                    Username = client.Name,
                    Color = client.Color
                });

                // Broadcast a full session user list.

                Broadcast(MessageBuilder.BuildParticipantListMessage(Clients.Values.ToList()));
            };

            OnClientLeft += (client) =>
            {
                // Broadcast to notify who left.

                Broadcast(new SessionUserLeft()
                {
                    Username = client.Name
                });

                // Broadcast a full session user list.

                Broadcast(MessageBuilder.BuildParticipantListMessage(Clients.Values.ToList()));
            };
        }

        public void AddClient(Client client)
        {
            Clients.TryAdd(client.SocketId, client);

            OnClientJoined?.Invoke(client);

            client.Session = this;

            client.OnClientDisconnected += (cl) =>
            {
                RemoveClient((Client)cl);
            };
        }

        public void RemoveClient(Client client)
        {
            Client dummy;
            Clients.TryRemove(client.SocketId, out dummy);

            dummy.Session = null;

            OnClientLeft?.Invoke(client);
        }

        public void Start()
        {
            if (sessionThread != null)
                return;

            this.sessionThread = new Thread(Run);
            this.sessionThread.IsBackground = true;
            this.sessionThread.Start();

            Console.WriteLine(string.Format("Session '{0}' started.", Name));

            OnSessionStarted?.Invoke(this);
        }

        private void Run()
        {
#if TEST_MODE
            // TODO: Remove - This is only for testing ...
            while (true)
#else
            while (Clients.Values.Count > 0)
#endif
            {
                foreach (Client client in Clients.Values)
                {
                    // client.Outbox.Enqueue("PING-Session");
                }

                Thread.Sleep(1000);
            }

            sessionThread = null;

            OnSessionEnded?.Invoke(this);

            Console.WriteLine(string.Format("Session '{0}' stopped.", Name));
        }

        /// <summary>
        /// Broadcast a message to all session clients. 
        /// </summary>
        /// <param name="message">The message.</param>
        public void Broadcast(Message message)
        {
            BroadcastWithExcept(message);
        }

        /// <summary>
        /// Broadcast a message to all session clients except the listed clients.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exceptClients"></param>
        public void BroadcastWithExcept(Message message, params Client[] exceptClients)
        {
            // Echo the message if it's not a MUSE message.

            foreach (var client in Clients.Values)
            {
                if (exceptClients != null && exceptClients.Any(e => e.SocketId == client.SocketId))
                {
                    continue;
                }

                client.SendStringAsMUSEFormat(message);
            }
        }
    }
}
