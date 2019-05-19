using MUSE.Server;
using NUnit.Framework;

namespace Tests
{
    public class NodeTest
    {
        [SetUp]
        public void Setup()
        {
        }

        private Node PrepareTestTree1()
        {
            Node root = new Node("Type", "Root");

            Node level1Child = root.AddChild(new Node("Type", "Level 1 - Child 1"));
            root.AddChild(new Node("Type", "Level 1 - Child 2"));
            root.AddChild(new Node("Type", "Level 1 - Child 3"));

            level1Child.AddChild(new Node("Type", "Level 2 - Child 1"));
            level1Child.AddChild(new Node("Type", "Level 2 - Child 2"));

            return root;
        }

        [Test]
        public void NodeDefaultConstructorTest()
        {
            Node node = new Node();            

            Assert.AreNotEqual(0, node.Id);
            Assert.AreNotEqual(new Node().Id, node.Id);
            Assert.AreEqual("", node.Name);
            Assert.Null(node.Parent);
            Assert.AreEqual("", node.Path.Path);
            Assert.AreEqual(1, node.Version);
        }

        [Test]
        public void NodeVersionUpdateTest()
        {
            Node root = new Node("Root");

            Assert.AreEqual(1, root.Version);

            root.Name = "A node name";

            Assert.AreEqual(2, root.Version);
        }

        [Test]
        public void GetNodeTest()
        {
            Node root = PrepareTestTree1();

            Assert.NotNull(root.GetNode("Root"));
            Assert.NotNull(root.GetNode("Root/Level 1 - Child 1"));
            Assert.NotNull(root.GetNode("Root/Level 1 - Child 1/Level 2 - Child 1"));
            Assert.Null(root.GetNode("Root/Level 2 - Child 1"));
        }
    }
}