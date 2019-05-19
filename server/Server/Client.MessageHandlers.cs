using MUSE.Server.Messages;
using MUSE.Server.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace MUSE.Server
{
    public partial class Client : ClientBase
    {
        /// <summary>
        /// A function to process a MUSE message.
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="server">The Server object.</param>
        /// <param name="client">The sender of the message.</param>
        /// <param name="session">The session the client belongs to.</param>
        /// <param name="message">The received message.</param>
        private delegate void MessageHandlerDelegate<T>(Server server, Client client, Session session, T message) where T : Message;

        /// <summary>
        /// The default message handler in case none of the registered message handlers can process the incoming message.
        /// </summary>
        private MessageHandlerDelegate<Message> DefaultMessageHandler;

        /// <summary>
        /// A dictionary with registered message handler for processing of specific messages.
        /// </summary>
        private ConcurrentDictionary<string, MessageHandlerDelegate<Message>> MessageHandler = new ConcurrentDictionary<string, MessageHandlerDelegate<Message>>();

        /// <summary>
        /// A dictionary with registered message handlers and their types.
        /// </summary>
        private ConcurrentDictionary<string, Type> MessageHandlerType = new ConcurrentDictionary<string, Type>();

        /// <summary>
        /// Registers several message handlers.
        /// </summary>
        private void RegisterMessageHandlers()
        {
            DefaultMessageHandler = BroadcastMessageHandler;

            RegisterMessageHandler<Ping>(PingMessageHandler);
            RegisterMessageHandler<ConnectionRequest>(AuthorizeMessageHandler);
            RegisterMessageHandler<SessionListRequest>(SessionListRequestMessageHandler);
            RegisterMessageHandler<SessionJoin>(SessionJoinMessageHandler);
            RegisterMessageHandler<SceneSynchronisationRequest>(SceneSynchronisationRequestHandler);
            RegisterMessageHandler<NodeAdd>(NodeAddMessageHandler);
            RegisterMessageHandler<NodeRemove>(NodeRemoveMessageHandler);
            RegisterMessageHandler<NodeUpdate>(NodeUpdateMessageHandler);
            RegisterMessageHandler<NodeSubscription>(NodeSubscriptionMessageHandler);
            RegisterMessageHandler<NodeReparent>(NodeReparentMessageHandler);
            RegisterMessageHandler<ResourceListRequest>(ResourceListRequestMessageHandler);
            RegisterMessageHandler<ResourceRequest>(ResourceRequestMessageHandler);
            RegisterMessageHandler<ResourceUpload>(ResourceUploadMessageHandler);
        }

        /// <summary>
        /// Registers a message handler for a specific message type.
        /// </summary>
        /// <typeparam name="T">The type of the message.</typeparam>
        /// <param name="handler">The message handler function.</param>
        private void RegisterMessageHandler<T>(MessageHandlerDelegate<T> handler) where T : Message
        {
            MessageHandler[typeof(T).Name] = (server, client, session, message) => 
            {
                handler(server, client, session, (T)message);
            };

            MessageHandlerType[typeof(T).Name] = typeof(T);
        }

        /// <summary>
        /// This handles incoming messages and delegates them to registered message handlers.
        /// </summary>
        /// <param name="msg">The incoming message.</param>
        override protected void HandleMessage(string msg)
        {
#if TEST_MODE
            Console.WriteLine("HandleMessage = " + msg);

            //using (StreamWriter sw = File.AppendText("D:\\muse_message_log.txt" ))
            //{
            //    var timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            //    sw.WriteLine(string.Format("{0} {1}", timestamp, msg));
            //}
#endif

            Message message = JsonUtils.DeserializeObject<Message>(msg);

            if (message == null)
            {
                Console.Error.WriteLine("The received message can not be processed.");
            }

            MessageHandlerDelegate<Message> messageHandler = DefaultMessageHandler;

            if (MessageHandler.ContainsKey(message._type))
            {
                Type messageType = MessageHandlerType[message._type];

                object obj = Activator.CreateInstance(messageType);

                MethodInfo methodInfo = messageType.GetMethod("AsMessage");

                methodInfo = methodInfo.MakeGenericMethod(messageType);

                message = (Message)methodInfo.Invoke(obj, new object[] { msg });

                messageHandler = MessageHandler[message._type];
            }

            messageHandler?.Invoke(server, this, Session, message);
        }

#region Message handler functions
                
        private void BroadcastMessageHandler(Server server, Client client, Session session, Message message)
        {
            server.SendBroadcastWithExcept(message, client);
        }

        private void PingMessageHandler(Server server, Client client, Session session, Ping message)
        {
            client.Send(message);
        }

        private void AuthorizeMessageHandler(Server server, Client client, Session session, ConnectionRequest message)
        {
            if (message.Magic != "#This is a magic string.#")
            {
                client.Disconnect();
            }

            client.Send(new ConnectionConfirm()
            {
                _rmid = message._smid
            });
        }
        
        private void SessionListRequestMessageHandler(Server server, Client client, Session session, SessionListRequest message)
        {
            client.Send(new SessionList()
            {
                Sessions = server.Sessions.Values.Select(e => e.Name).ToList()
            });
        }

        private void SessionJoinMessageHandler(Server server, Client client, Session session, SessionJoin message)
        {
            client.Name = message.Username;
            client.Color = message.UserColor;

            Console.WriteLine(string.Format("User '{0}' wants to session '{1}'.", message.Username, message.Session));

            Session sessionToJoin = server.GetOrCreateSession(message.Session);
            sessionToJoin.AddClient(client);
            sessionToJoin.Start();

            Console.WriteLine(string.Format("User '{0}' joins session '{1}'.", message.Username, message.Session));

#if TEST_MODE
            bool isSessionOwner = sessionToJoin.Clients.Values.Count == 1 && sessionToJoin.Node == null;
#else
            // The first user within the session is also the creater of the session and session owner.

            bool isSessionOwner = sessionToJoin.Clients.Values.Count == 1;
#endif

            if (isSessionOwner)
            {
                sessionToJoin.Owner = client;
                sessionToJoin.Password = message.SessionPassword;
                sessionToJoin.FileSync = message.SessionFileSync;
            }
            else
            {
                if (Session.Password != message.SessionPassword)
                {
                    client.Send(new SessionJoinDeclined()
                    {
                        _rmid = message._smid,
                        Message = "Unauthorized."
                    });

                    Thread.Sleep(1000);

                    client.Disconnect();

                    return;
                }
            }

            client.Send(new SessionJoined()
            {
                _rmid = message._smid,
                Owner = isSessionOwner
            });

            if (sessionToJoin.FileSync)
            {
                if (isSessionOwner && sessionToJoin.FileSync)
                {
                    client.Send(new ResourceListRequest()
                    {
                    });
                }
                else
                {
                    Client sessionOwner = sessionToJoin.Owner;
                    
                    foreach (var resource in sessionOwner.UserResources)
                    {
                        client.Send(new ResourceUpload()
                        {
                            FilePath = resource.FilePath,
                            Content = Convert.ToBase64String(resource.Data),
                            Hash = resource.Hash
                        });
                    }
                }
            }
        }

        private void SceneSynchronisationRequestHandler(Server server, Client client, Session session, Message message)
        {
            Queue<Node> nodesToSynchronise = new Queue<Node>();

            nodesToSynchronise.Enqueue(session.Node);

            while (nodesToSynchronise.Count > 0)
            {
                Node current = nodesToSynchronise.Dequeue();

                client.Send(new SceneSynchronisation()
                {
                    ParentSelector = current.Path.ParentPath,
                    Type = current.Type,
                    Name = current.Name,

                    Properties = current.GetProperties().Select(p => new NodeProperty()
                    {
                        Name = p.Name,
                        Data = p.GetDataAsUTF8String()
                    }).ToList()
                });

                current.GetChildrenWhereTypeStartWith("Node:").ForEach(e => nodesToSynchronise.Enqueue(e));
            }
        }

        private void NodeAddMessageHandler(Server server, Client client, Session session, NodeAdd message)
        {
            Console.WriteLine(message.Name);

            if (session.Node == null || string.IsNullOrWhiteSpace(message.ParentSelector))
            {
                session.Node = new Node(message.Type, message.Name);

                if (message.Properties != null)
                {                   
                    foreach (var p in message.Properties)
                    {
                        session.Node.AddPropertyAsChild(p.Name, p.Data);
                    }
                }
                return;
            }

            Node foundNode = session.Node.GetNode(message.ParentSelector);
            Node newNode = foundNode.AddChild(new Node(message.Type, message.Name));

            if (message.Properties != null)
            {
                message.Properties.ForEach(p =>
                {
                    newNode.AddPropertyAsChild(p.Name, p.Data);
                });
            }

            // Forward the update message to all clients.

            server.SendBroadcastWithExcept(message, client);
        }

        private void NodeRemoveMessageHandler(Server server, Client client, Session session, NodeRemove message)
        {
            // Update internal state.

            Node node = session.Node.GetNode(new NodePath(message.Selector));

            if (node == null)
                return;

            NodePath nodePath = new NodePath(message.Selector);

            node.Parent.Children.RemoveAll(m => m.Path.Path == nodePath.Path);

            // Forward the update message to all clients.

            server.SendBroadcastWithExcept(message, client);
        }

        private void NodeUpdateMessageHandler(Server server, Client client, Session session, NodeUpdate message)
        {
            // Update internal state.

            Node node = session.Node.GetNode(new NodePath(message.Selector));

            if (node == null)
                return;

            if (message.Properties == null)
                return;

            message.Properties.ForEach(p =>
            {
                Node propertyNode = node.GetProperty(p.Name); ;

                if (propertyNode == null)
                    return;

                propertyNode.SetData(p.Data);
            });

            // Forward the update message to all clients.

            server.SendBroadcastWithExcept(message, client);
        }

        private void NodeSubscriptionMessageHandler(Server server, Client client, Session session, NodeSubscription message)
        {
            // Update internal state.

            Node node = session.Node.GetNode(new NodePath(message.Selector));

            if (node == null)
                return;

            if (node.Subscriptions.Contains(client) == message.SubscriptionStatus == false)
            {
                node.Subscriptions.Remove(client);
            }
            else if (message.SubscriptionStatus)
            {
                node.Subscriptions.Add(client);
            }
        }

        private void NodeReparentMessageHandler(Server server, Client client, Session session, NodeReparent message)
        {
            Node node = session.Node.GetNode(new NodePath(message.Selector));

            if (node == null)
                return;

            Node newParent = session.Node.GetNode(new NodePath(message.ParentSelector));

            if (newParent == null)
                return;

            if (node.Parent != null)
                node.Parent.Children.Remove(node);

            newParent.AddChild(node);
        }

        public void HandleNodeEvent(NodeEventBase nodeEvent)
        {
            if (nodeEvent is NodeDataChangedEvent)
            {
                NodeDataChangedEvent nodeDataChanged = nodeEvent as NodeDataChangedEvent;

                this.Send(new NodeEvent()
                {
                    Type = nodeDataChanged.GetType().Name
                });

                this.Send(new NodeUpdate()
                {
                    Selector = nodeDataChanged.Node.Path.ParentPath,
                    Properties = new List<NodeProperty> {
                        new NodeProperty()
                        {
                            Name = nodeDataChanged.Node.Path.ParentPath,
                            Data = nodeDataChanged.Node.GetDataAsUTF8String()
                        }
                    }
                });
            }
        }

        private void ResourceListRequestMessageHandler(Server server, Client client, Session session, ResourceListRequest message)
        {
            Client sessionOwner = session.Owner;

            foreach (var resource in sessionOwner.UserResources)
            {
                client.Send(new ResourceUpload()
                {
                    FilePath = resource.FilePath,
                    Hash = resource.Hash
                });
            }
        }

        private void ResourceUploadMessageHandler(Server server, Client client, Session session, ResourceUpload message)
        {
            ResourceData ressourceObject = null;

            if (session.RESOURCE_DATA_CACHE.TryGetValue(message.Hash, out ressourceObject))
            {
                ResourceFile ressource = new ResourceFile
                {
                    FilePath = message.FilePath,
                    Data = ressourceObject.Data,
                    Hash = ressourceObject.Hash
                };

                client.UserResources.Add(ressource);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(message.Content))
                {
                    client.Send(new ResourceRequest
                    {
                        FilePath = message.FilePath,
                        Hash = message.Hash
                    });
                }
                else
                {
                    byte[] bytes = Convert.FromBase64String(message.Content);

                    Console.WriteLine("New file received");
                    Console.WriteLine("File path: " + message.FilePath);
                    Console.WriteLine("File size: " + bytes.Length);

                    ressourceObject = new ResourceData
                    {
                        Data = bytes,
                        Hash = message.Hash
                    };

                    ResourceFile ressource = new ResourceFile
                    {
                        FilePath = message.FilePath,
                        Data = bytes,
                        Hash = message.Hash
                    };

                    client.UserResources.Add(ressource);
                    session.RESOURCE_DATA_CACHE.Add(message.Hash, ressourceObject);

                    Console.WriteLine("Total files in users repository:" + client.UserResources.Count);
                }
            }
        }
        
        private void ResourceRequestMessageHandler(Server server, Client client, Session session, ResourceRequest message)
        {
            foreach (Client s in server.Clients.Values)
            {
                foreach (var resource in s.UserResources)
                {
                    if (resource.FilePath == message.FilePath)
                    {
                        client.Send(new ResourceUpload()
                        {
                            FilePath = resource.FilePath,
                            Content = Convert.ToBase64String(resource.Data),
                            Hash = resource.Hash
                        });
                    }
                }
            }
        }
        
        #endregion
    }
}
