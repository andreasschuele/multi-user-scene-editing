using MUSE.Server;
using NUnit.Framework;

namespace Tests
{
    public class NodePathTest
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void NodePathConstructorWithSimplePathTest()
        {
            NodePath nodePath = new NodePath("Root");

            Assert.AreEqual(nodePath.Path, "Root");
            Assert.AreEqual(nodePath.ParentPath, "");
            Assert.AreEqual(nodePath.Sections.Count, 1);
            Assert.AreEqual(nodePath.Sections[0], "Root");
        }

        [Test]
        public void NodePathConstructorWithComplexPathTest()
        {
            NodePath nodePath = new NodePath("Root/Node1/Node2/Node3");

            Assert.AreEqual(nodePath.Path, "Root/Node1/Node2/Node3");
            Assert.AreEqual(nodePath.ParentPath, "Root/Node1/Node2");
            Assert.AreEqual(nodePath.Sections.Count, 4);
            Assert.AreEqual(nodePath.Sections[0], "Root");
            Assert.AreEqual(nodePath.Sections[1], "Node1");
            Assert.AreEqual(nodePath.Sections[2], "Node2");
            Assert.AreEqual(nodePath.Sections[3], "Node3");
        }

        [Test]
        public void NodePathConstructorFromNodeTest()
        {
            Node root = new Node("Type", "Root");
            Node node1 = root.AddChild(new Node("Type", "Node1"));
            Node node2 = node1.AddChild(new Node("Type", "Node2"));
            Node node3 = node2.AddChild(new Node("Type", "Node3"));

            NodePath nodePath = new NodePath(node3);

            Assert.AreEqual(nodePath.Path, "Root/Node1/Node2/Node3");
            Assert.AreEqual(nodePath.ParentPath, "Root/Node1/Node2");
            Assert.AreEqual(nodePath.Sections.Count, 4);
            Assert.AreEqual(nodePath.Sections[0], "Root");
            Assert.AreEqual(nodePath.Sections[1], "Node1");
            Assert.AreEqual(nodePath.Sections[2], "Node2");
            Assert.AreEqual(nodePath.Sections[3], "Node3");
        }
    }
}