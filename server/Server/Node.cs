using MUSE.Server.Middleware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MUSE.Server
{
    /// <summary>
    /// The Node class can store all relevant information of a scene graph.
    /// </summary>
    public class Node
    {
        #region Public attributes.

        /// <summary>
        /// A unique node id.
        /// </summary>
        public long Id
        {
            get { return id; }
        }

        /// <summary>
        /// A custom node type.
        /// </summary>
        public string Type
        {
            get { return type; }
            set
            {
                type = value;
                version++;
            }
        }

        /// <summary>
        /// A custom node name.
        /// </summary>
        public string Name
        {
            get { return name; }
            set
            {
                var nameOld = name;

                this.name = value;
                this.version++;

                fireEvent(new NodeNameChangedEvent()
                {
                    Node = this,
                    NameOld = nameOld,
                    NameNew = this.name
                });
            }
        }

        /// <summary>
        /// The path to this node.
        /// </summary>
        public NodePath Path
        {
            get { return nodePath; }
        }

        /// <summary>
        /// A list with children of this node.
        /// </summary>
        public List<Node> Children
        {
            get { return children; }
        }

        /// <summary>
        /// The root node.
        /// </summary>
        public Node Root
        {
            get
            {
                Node root = this;

                Queue<Node> queue = new Queue<Node>();
                queue.Enqueue(this);

                while(queue.Count > 0)
                {
                    Node n = queue.Dequeue();

                    if (n.Parent != null)
                    {
                        root = n.Parent;

                        queue.Enqueue(n.Parent);
                    }
                }

                return root;
            }
        }

        /// <summary>
        /// The parent node.
        /// </summary>
        public Node Parent
        {
            get { return parent;  }
            set
            {
                var parentOld = this.parent;

                this.parent = value;
                this.version++;
                this.nodePath = new NodePath(this);

                fireEvent(new NodeParentChangedEvent()
                {
                    Node = this,
                    ParentOld = parentOld,
                    ParentNew = this.parent
                });
            }
        }

        /// <summary>
        /// The data that this node contains.
        /// </summary>
        public byte[] Data
        {
            get { return data;  }
            set
            {           
                this.data = value;
                this.hash = Encoding.UTF8.GetString(System.Security.Cryptography.MD5.Create().ComputeHash(data));
                this.version++;

                fireEvent(new NodeDataChangedEvent()
                {
                    Node = this,
                    Data = this.data
                });
            }
        }

        /// <summary>
        /// The hash of the data that this node conatins.
        /// </summary>
        public string Hash
        {
            get { return hash; }
        }

        /// <summary>
        /// The internal version state of this node.
        /// </summary>
        public int Version
        {
            get
            {
                return version;
            }
        }

        /// <summary>
        /// The node owner.
        /// </summary>
        public Client Owner
        {
            get { return owner; }
            set
            {
                var ownerOld = this.owner;

                this.owner = value;

                fireEvent(new NodeOwnerChangedEvent()
                {
                    Node = this,
                    OwnerOld = ownerOld,
                    OwnerNew = owner
                });
            }
        }

        /// <summary>
        /// The node owner.
        /// </summary>
        public Client Locked
        {
            get { return locked; }
            set
            {
                var lockedByOld = this.locked;

                this.locked = value;

                fireEvent(new NodeLockStatusChangedEvent()
                {
                    Node = this,
                    LockedByOld = lockedByOld,
                    LockedByNew = locked
                });
            }
        }

        public List<Client> Subscriptions
        {
            get { return subscriptions; }
        }

        #endregion

        #region Private attributes.

        private static long NODE_ID = 1;

        private long id = NODE_ID++;

        private string type;

        private string name = "";

        private NodePath nodePath;

        private Client owner;

        private Client locked;

        private Node parent;

        private List<Node> children = new List<Node>();

        private byte[] data;

        private string hash;

        private int version = 1;

        private List<Client> subscriptions = new List<Client>();

        #endregion

        #region Constructors

        public Node()
        {
            this.nodePath = new NodePath(this);
        }

        public Node(string type) : this()
        {
            this.type = type;
        }

        public Node(string type, string name) : this()
        {
            this.name = name;
            this.type = type;
        }

        public Node(string type, string name, byte[] data) : this()
        {
            this.name = name;
            this.type = type;
            this.data = data;
        }

        #endregion

        public Node AddChild(Node node)
        {
            children.Add(node);
            node.Parent = this;

            fireEvent(new NodeChildAddedEvent()
            {
                Parent = this,
                Child = node
            });

            return node;
        }

        public Node AddChild(string type, string name, byte[] data)
        {
            return AddChild(new Node(type, name, data));
        }

        public void RemoveChild(Node node)
        {
            if (Children.Contains(node) == false)
                return;

            Children.Remove(node);

            fireEvent(new NodeChildRemovedEvent()
            {
                Parent = this,
                Child = node
            });
        }

        public List<Node> GetChildrenOfType(string type)
        {
            return Children.Where(p => p.Type == type).ToList();
        }

        public List<Node> GetChildrenWhereTypeStartWith(string type)
        {
            return Children.Where(p => p.Type.StartsWith(type)).ToList();
        }

        /// <summary>
        /// Adds a proeprty as a child node.
        /// </summary>
        /// <param name="name">Property name</param>
        /// <param name="value">Property value</param>
        /// <returns></returns>
        public Node AddPropertyAsChild(string name, string value)
        {
            Node propertyNode = GetProperty(name);

            if (propertyNode == null)
            {
                propertyNode = new Node()
                {
                    Type = "Property",
                    Name = name
                };

                AddChild(propertyNode);
            }

            propertyNode.SetData(value);

            return propertyNode;
        }

        public List<Node> GetProperties()
        {
            return GetChildrenOfType("Property");
        }

        public Node GetProperty(string name)
        {
            return GetProperties().Find(node => node.Name == name);
        }


        public Node GetNode(string selector)
        {
            return GetNode(new NodePath(selector));
        }

        public Node GetNode(NodePath selector)
        {
            if (selector == null || selector.Path == null)
                return null;

            if (selector.Path == "")
                return null;

            Queue<Node> searchIn = new Queue<Node>();
            searchIn.Enqueue(Root);

            Node toCheck = null;

            for (int idx = 0; idx < selector.Sections.Count; idx++)
            {
                while(searchIn.Count > 0)
                {
                    toCheck = searchIn.Dequeue();

                    if (toCheck.Name == selector.Sections[idx])
                    {
                        if (idx == selector.Sections.Count - 1)
                        {
                            return toCheck;
                        }

                        searchIn.Clear();
                        toCheck.Children.ForEach(e => searchIn.Enqueue(e));

                        break;
                    }
                }
            }

            return null;
        }

        public void SetData(string value)
        {
            Data = Encoding.UTF8.GetBytes(value);
        }

        public string GetDataAsUTF8String()
        {
            return Encoding.UTF8.GetString(Data);
        }

        protected void fireEvent(NodeEventBase nodeEvent)
        {
            List<Client> subscribers = subscriptions;

            foreach (var client in subscribers)
            {
                try
                {
                    client.HandleNodeEvent(nodeEvent);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message, e.StackTrace);
                }
            }
        }
    }
}
