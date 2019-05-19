using MUSE.Server.Messages;
using MUSE.Server.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace MUSE.Server
{
    public class ResourceData
    {
        public string Hash;

        public byte[] Data;
    }
    
    public class Server
    {
        public delegate void ClientEvent(Client context);

        public event ClientEvent OnClientAdded;
        public event ClientEvent OnClientRemoved;

        /// <summary>
        /// Sessions hosted on this server.
        /// </summary>
        public ConcurrentDictionary<string, Session> Sessions = new ConcurrentDictionary<string, Session>();

        /// <summary>
        /// All clients that are connected to this server.
        /// </summary>
        public ConcurrentDictionary<string, Client> Clients = new ConcurrentDictionary<string, Client>();

        /// <summary>
        /// Server thread.
        /// </summary>
        private Thread serverThread;

        public Server()
        {
            this.serverThread = new Thread(Run);
            this.serverThread.IsBackground = true;
            this.serverThread.Start();
        }

        public void AddClient(Client client)
        {
            client.StartThreads();

            Clients.TryAdd(client.SocketId, client);

            // Process client connected events.

            OnClientAdded?.Invoke(client);
        }

        public void RemoveClient(Client client)
        {
            Client dummy;
            Clients.TryRemove(client.SocketId, out dummy);

            // Process client disconnected event.

            client.OnClientRemoved();

            OnClientRemoved?.Invoke(client);
        }

        public Session NewSession(string name)
        {
            Session session = new Session(name);

            Sessions[name] = session;

            return session;
        }

        public Session GetSession(string name)
        {
            Session session;

            if (Sessions.TryGetValue(name, out session))
            {
                return session;
            }

            return null;
        }

        public Session GetSession(Node node)
        {
            return Sessions.Values.Where(p => p.Node == node).First();
        }

        public Session GetOrCreateSession(string name)
        {
            Session session = GetSession(name);

            if (session != null)
                return session;

            return NewSession(name);
        }

        public void SendBroadcastWithExcept(Message message, params Client[] exceptClients)
        {
            foreach (var client in Clients.Values)
            {
                if (exceptClients != null && exceptClients.Any(e => e.SocketId == client.SocketId))
                {
                    continue;
                }

                client.Send(message);
            }
        }

        public void SendBroadcast(Message message)
        {
            SendBroadcastWithExcept(message);
        }

        private void Run()
        {
            while (true)
            {
                foreach (Client client in Clients.Values)
                {
                    client.Outbox.Enqueue("PING");
                }

                Thread.Sleep(1000);
            }
        }
    }
}
