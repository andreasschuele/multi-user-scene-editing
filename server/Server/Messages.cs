using MUSE.Server.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MUSE.Server.Messages
{

    /// <summary>
    /// The base class for all messages.
    /// </summary>
    public class Message
    {
        public int _smid { get; set; }

        public int _rmid { get; set; }

        public string _type { get; set; }
        
        public long _time { get; set; }
        
        public Message()
        {
            _type = this.GetType().Name;
        }

        public string AsJson()
        {
            return JsonUtils.SerializeObject(this);
        }

        public T AsMessage<T>(string msg)
        {
            return JsonUtils.DeserializeObject<T>(msg);
        }
    }

    /// <summary>
    /// A message container to transfer multiple messages at once.
    /// </summary>
    public class MessageContainer : Message
    {
        public MessageContainer()
        {
            Messages = new List<Message>();
        }

        public List<Message> Messages { get; set; }
    }

    /// <summary>
    /// A simple ping message with a timestamp.
    /// </summary>
    public class Ping : Message
    {
        public int Timestamp { get; set; }
    }

    /// <summary>
    /// The message is sent by the client to request access to the server. Its content is
    /// a magic string to authenticate to the server as a MUSE add-on.
    /// </summary>
    public class ConnectionRequest : Message
    {
        public string Magic { get; set; }
    }

    /// <summary>
    /// If the server approves the connection, it replies with this message.
    /// </summary>
    public class ConnectionConfirm : Message
    {
    }

    /// <summary>
    /// This message is sent by the client to request a list of all hosted sessions.
    /// </summary>
    public class SessionListRequest : Message
    {
    }

    /// <summary>
    /// This message is sent as response to the  SessionListRequest message. As the name 
    /// indicates, the message contains a list of session names.
    /// </summary>
    public class SessionList : Message
    {
        public List<string> Sessions { get; set; }
    }

    /// <summary>
    /// This message is sent by the client with the request to join a session. If the 
    /// session does not yet exist, a new session is created and the client is 
    /// entered as the session owner. 
    /// </summary>
    public class SessionJoin : Message
    {
        public string Session { get; set; }

        public string SessionPassword { get; set; }

        public bool SessionFileSync { get; set; }

        public string Username { get; set; }

        public string UserColor { get; set; }
    }

    /// <summary>
    /// This message is sent as response to the  SessionJoin message.
    /// </summary>
    public class SessionJoined : Message
    {
        public bool Owner { get; set; }
    }

    /// <summary>
    /// This message is sent as response to the  SessionJoin message if the server has 
    /// declied the request because the session password does not match.
    /// </summary>
    public class SessionJoinDeclined : Message
    {
        public string Message { get; set; }
    }

    /// <summary>
    /// This message contains a list of all session participants. It is sent by the 
    /// server to all session participants after a participant has joined or left a session.
    /// </summary>
    public class SessionUserList : Message
    {
        public class User
        {
            public string Username;
            public string Color;
        }

        public SessionUserList()
        {
            Users = new List<User>();
        }

        public IList<User> Users;
    }

    /// <summary>
    /// This message is broadcasted by the server once a participant joins the session.
    /// </summary>
    public class SessionUserJoined : Message
    {
        public string Username { get; set; }
        public string Color { get; set; }
    }

    /// <summary>
    /// This message is broadcasted by the server once a participant leaves the session.
    /// </summary>
    public class SessionUserLeft : Message
    {
        public string Username { get; set; }
    }

    public class NodeProperty
    {
        public string Name { get; set; }
        public string Data { get; set; }
    }

    /// <summary>
    /// This message is broadcasted to inform it that a new node has been added. 
    /// </summary>
    public class NodeAdd : Message
    {
        public string ParentSelector { get; set; }

        public string Type { get; set; }

        public string Name { get; set; }

        public string Data { get; set; }

        public List<NodeProperty> Properties { get; set; }
    }

    /// <summary>
    /// This message is broadcasted to inform that a node has been removed.
    /// </summary>
    public class NodeRemove : Message
    {
        public string Selector { get; set; }
    }

    /// <summary>
    /// This message is broadcasted to inform about changes in the node data 
    /// e.g. changes of properties. 
    /// </summary>
    public class NodeUpdate : Message
    {
        public string Selector { get; set; }

        public List<NodeProperty> Properties { get; set; }
    }

    /// <summary>
    /// This message is sent by a client to subscribe or unsubscribe for events 
    /// on a particular node.
    /// </summary>
    public class NodeSubscription : Message
    {
        public string Selector { get; set; }

        public bool SubscriptionStatus { get; set; }
    }

    /// <summary>
    /// This message is sent by a client to reparent a node.
    /// </summary>
    public class NodeReparent : Message
    {
        public string ParentSelector { get; set; }

        public string Selector { get; set; }
    }
    
    public class NodeEvent : Message
    {
        public string Type { get; set; }

    }

    /// <summary>
    /// This message is sent to request an initial scene synchronization.
    /// </summary>
    public class SceneSynchronisationRequest : Message
    {
    }

    /// <summary>
    /// This message is sent by the client to initially upload the scene state. 
    /// </summary>
    public class SceneSynchronisation : Message
    {
        public string ParentSelector { get; set; }

        public string Type { get; set; }

        public string Name { get; set; }

        public string Data { get; set; }

        public List<NodeProperty> Properties { get; set; }
    }

    /// <summary>
    /// This message is sent either by the client or server to request a list of 
    /// resources. As response the ResourceUpload message is sent without binary content.
    /// </summary>
    public class ResourceListRequest : Message
    {
    }

    /// <summary>
    /// This message is sent by the client or server to request the content of a 
    /// certain resource. As response the ResourceUpload message is sent with binary content.
    /// </summary>
    public class ResourceRequest : Message
    {
        public string FilePath { get; set; }

        public string Hash { get; set; }
    }

    /// <summary>
    /// This message is sent as response to the ResourceListRequest and RessourceRequest 
    /// message and contains the filename, filepath, hash and optionally the content of the file.
    /// </summary>
    public class ResourceUpload : Message
    {
        public string FileName { get; set; }

        public string FilePath { get; set; }

        public string Content { get; set; }

        public string Hash { get; set; }
    }

}
