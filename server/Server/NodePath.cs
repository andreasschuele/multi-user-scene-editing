using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MUSE.Server
{
    /// <summary>
    /// The NodePath class contains the path to a specific node.
    /// </summary>
    public class NodePath
    {
        /// <summary>
        /// The node path separator.
        /// </summary>
        public static readonly string PATH_SEPARATOR = "/";

        /// <summary>
        /// A list with all sections of a node path.
        /// </summary>
        public List<string> Sections { get; }

        /// <summary>
        /// The full node path.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// The full path to the parent node.
        /// </summary>
        public string ParentPath { get; }

        /// <summary>
        /// Constructs a NodePath object based on a full node path.
        /// </summary>
        /// <param name="path">A full path to a node.</param>
        public NodePath(string path)
        {
            this.Path = path;
            this.Sections = Path.Split(PATH_SEPARATOR).ToList();
            this.ParentPath = "";

            if (Sections.Count >= 2)
            {
                this.ParentPath = string.Join(PATH_SEPARATOR, Sections.Take(Sections.Count - 1));
            }
        }

        /// <summary>
        /// Constructs a NodePath based on a Node object.
        /// </summary>
        /// <param name="node">A Node object.</param>
        public NodePath(Node node) : this(BuildPath(node))
        {
        }

        /// <summary>
        /// Returns the full node path.
        /// </summary>
        /// <returns>Returns the full node path.</returns>
        override public string ToString()
        {
            return Path;
        }

        /// <summary>
        /// Builds recursively a node path based on given node.
        /// </summary>
        /// <param name="node">A Node object.</param>
        /// <returns>Returns the full node path.</returns>
        private static string BuildPath(Node node)
        {
            if (node == null)
                return "";

            // Root level
            if (node.Parent == null)
                return node.Name;
                        
            // A / B
            return BuildPath(node.Parent) + PATH_SEPARATOR + node.Name;
        }
    }
}
