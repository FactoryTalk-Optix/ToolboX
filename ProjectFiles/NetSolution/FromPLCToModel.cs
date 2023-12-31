#region Using directives
using UAManagedCore;
using FTOptix.HMIProject;
using FTOptix.Core;
using FTOptix.NetLogic;
using FTOptix.CommunicationDriver;
using System.Linq;
using FTOptix.Alarm;
using FTOptix.CODESYS;
using utilx.Utils;
using FTOptix.DataLogger;
using FTOptix.S7TiaProfinet;
using FTOptix.RAEtherNetIP;
using FTOptix.OPCUAServer;
#endregion

public class FromPLCToModel : BaseNetLogic
{
    [ExportMethod]
    public void GenerateNodesIntoModel()
    {
        var startingNode = InformationModel.Get(LogicObject.GetVariable("StartingNodeToFetch").Value);
        UtilsTags utilsTags = new UtilsTags(LogicObject, startingNode);
        utilsTags.GenerateNodesIntoModel();
    }
}

