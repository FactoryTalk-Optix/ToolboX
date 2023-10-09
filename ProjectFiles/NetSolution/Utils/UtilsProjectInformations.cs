#region Using directives
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using System.Collections.Generic;
using System.Linq;
using FTOptix.NetLogic;
using System;
#endregion

namespace utilx.Utils
{
    internal class UtilsProjectInformations
    {
        private readonly IUAObject _logicObject;
        private const string _MESSAGE_PROJECT_TAGS_NUMBER = "Total tags:";
        private const string _MESSAGE_PROJECT_UNREFERENCED_TAGS_NUMBER = "Total unreferenced tags:";
        private const string _MESSAGE_PROJECT_BROKEN_DYNAMIC_LINKS = "Broken dynamic links:";
        private const string _MESSAGE_PROJECT_NODES_NUMEBER = "Project nodes:";
        private const string _MESSAGE_PROJECT_NET_LOGIC_METHODS = "Project C# methods:";
        private const string _LOG_PREFIX = "--- ";

        public List<FTOptix.CommunicationDriver.Tag> Tags { get; set; }
        public List<FTOptix.CommunicationDriver.Tag> UnreferencedTags { get; set; }
        public List<IUANode> BrokenDynamicLinks { get; set; }

        public int NodesCount { get; set; }

        public List<string> ProjectNetLogicMethods { get; set; }

        public UtilsProjectInformations(IUAObject logicObject)
        {
            _logicObject = logicObject;
        }

        /// <summary>
        /// Gets the project infos.
        /// </summary>
        public void GetProjectInfos()
        {
            var nodesWithDLink = GetAllNodesWithDynamicLink();
            var referencedNodes = GetAllReferencedNodes(nodesWithDLink);

            NodesCount = GetProjectNodesNumber();
            Tags = GetAllTags();
            UnreferencedTags = Tags.Where(t => !referencedNodes.Select(n => n.NodeId).Contains(t.NodeId)).ToList();
            BrokenDynamicLinks = GetBrokenDynamicLinks(nodesWithDLink);
            ProjectNetLogicMethods = GetAllScriptMethods();

            LogProjectInfos();
        }

        /// <summary>
        /// Gets the nodes number starting from a specific node
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <returns>An int.</returns>
        public static int GetNodesNumber(NodeId nodeId) => InformationModel.Get(nodeId).FindNodesByType<IUANode>().Count();

        /// <summary>
        /// Logs the project infos.
        /// </summary>
        public void LogProjectInfos()
        {

            foreach (var t in UnreferencedTags) Log.Info($"Unreferenced {t.BrowseName} {Log.Node(t)}");
            Log.Info($"{_LOG_PREFIX} {_MESSAGE_PROJECT_UNREFERENCED_TAGS_NUMBER} {UnreferencedTags.Count}");
            foreach (var t in BrokenDynamicLinks) Log.Info($"{Log.Node(t)} has broken link: {t.GetVariable("DynamicLink").Value.Value}");
            Log.Info($"{_LOG_PREFIX} {_MESSAGE_PROJECT_BROKEN_DYNAMIC_LINKS} {BrokenDynamicLinks.Count}");
            Log.Info($"{_LOG_PREFIX} {_MESSAGE_PROJECT_TAGS_NUMBER} {Tags.Count}");
            Log.Info($"{_LOG_PREFIX} {_MESSAGE_PROJECT_NODES_NUMEBER} {NodesCount}");
            Log.Info($"{_LOG_PREFIX} {_MESSAGE_PROJECT_NET_LOGIC_METHODS}");
            foreach (var t in ProjectNetLogicMethods) Log.Info($"{t}");
        }

        /// <summary>
        /// Gets the project nodes number.
        /// </summary>
        /// <returns>An int.</returns>
        public static int GetProjectNodesNumber() => Project.Current.Parent.Owner.FindNodesByType<IUANode>().Count();

        /// <summary>
        /// Retrieve all project's C# methods
        /// </summary>
        /// <returns></returns>
        public static List<string> GetAllScriptMethods()
        {
            var res = new List<string>();
            var netLogicObjects = Project.Current.Parent.FindNodesByType<FTOptix.NetLogic.NetLogicObject>().ToList();
            foreach (var script in netLogicObjects)
            {
                var methods = script.Children.Where(s => s is UAMethod).Select(s => s.BrowseName);
                res.Add(script.BrowseName + " has methods: " + string.Join(", ", methods));
            }

            return res;
        }

        #region private methods

        /// <summary>
        /// Gets the all tags in the current project
        /// </summary>
        /// <returns>A list of FTOptix.CommunicationDriver.Tag.</returns>
        private static List<FTOptix.CommunicationDriver.Tag> GetAllTags() => Project.Current.Parent.FindNodesByType<FTOptix.CommunicationDriver.Tag>().ToList();

        /// <summary>
        /// Gets the all nodes with dynamic link != null
        /// </summary>
        /// <returns>A list of IUANodes.</returns>
        private static List<IUANode> GetAllNodesWithDynamicLink() => Project.Current.Parent.FindNodesByType<IUANode>().Where(n => { return n.GetVariable("DynamicLink") != null; }).ToList();

        /// <summary>
        /// Gets the all referenced nodes.
        /// </summary>
        /// <param name="nodesWithDlink">The nodes with dlink.</param>
        /// <returns>A list of IUANodes.</returns>
        private List<IUANode> GetAllReferencedNodes(List<IUANode> nodesWithDlink) => nodesWithDlink.Select(rn => GetReferencedNode(rn)).Where(rn => rn is not null).ToList();

        /// <summary>
        /// Gets the broken dynamic links.
        /// </summary>
        /// <param name="nodesWithDlink">The nodes with dlink.</param>
        /// <returns>A list of IUANodes.</returns>
        private List<IUANode> GetBrokenDynamicLinks(List<IUANode> nodesWithDlink) => nodesWithDlink.Where(rn => GetReferencedNode(rn) is null).ToList();

        /// <summary>
        /// Gets the referenced node by a dynamic link of a given node
        /// </summary>
        /// <param name="uANode">The u a node.</param>
        /// <returns>An IUANode.</returns>
        private IUANode GetReferencedNode(IUANode uANode)
        {
            var dl = uANode.GetVariable("DynamicLink");
            if (dl == null) { return null; }
            return _logicObject.Context.ResolvePath(uANode, dl.Value.Value.ToString()).ResolvedNode;
        }

        #endregion

    }
}
