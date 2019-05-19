using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MUSE.Server
{
    public class NodeEventBase
    {
    
    }

    public class NodeChildAddedEvent : NodeEventBase
    {
        public Node Parent;
        public Node Child;
    }

    public class NodeChildRemovedEvent : NodeEventBase
    {
        public Node Parent;
        public Node Child;
    }

    public class NodeParentChangedEvent : NodeEventBase
    {
        public Node Node;
        public Node ParentOld;
        public Node ParentNew;
    }

    public class NodeNameChangedEvent : NodeEventBase
    {
        public Node Node;
        public string NameOld;
        public string NameNew;
    }

    public class NodeOwnerChangedEvent : NodeEventBase
    {
        public Node Node;
        public Client OwnerOld;
        public Client OwnerNew;
    }

    public class NodeLockStatusChangedEvent : NodeEventBase
    {
        public Node Node;
        public Client LockedByOld;
        public Client LockedByNew;
    }

    public class NodeDataChangedEvent : NodeEventBase
    {
        public Node Node;
        public byte[] Data;
    }
}
