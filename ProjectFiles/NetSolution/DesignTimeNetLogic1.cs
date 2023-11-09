#region Using directives
using FTOptix.NetLogic;
using utilx.Utils;
using FTOptix.Recipe;
using FTOptix.SQLiteStore;
using FTOptix.Store;
using FTOptix.EventLogger;
using System;
using UAManagedCore;
using FTOptix.HMIProject;
using FTOptix.Core;
using FTOptix.CODESYS;
using FTOptix.DataLogger;
using FTOptix.S7TiaProfinet;
using FTOptix.RAEtherNetIP;
using FTOptix.OPCUAServer;
#endregion

public partial class DesignTimeNetLogic1 : BaseNetLogic
{
    [ExportMethod]
    public void GetProjectInfos()
    {
        UtilsProjectInformations projectInformations = new UtilsProjectInformations(LogicObject);
        projectInformations.GetProjectInfos();
    }

    [ExportMethod]
    public void GetProjectInfosElapsedTime()
    {
        Func<int> myMethod = () => UtilsProjectInformations.GetProjectNodesNumber();
        Utils.MeasureMethodExecutionTime(myMethod);
    }

    [ExportMethod]  
    public void DeleteAllAlarm()
    {
        UtilsAlarms utilsAlarms = new UtilsAlarms(LogicObject);     
        utilsAlarms.ClearAlarmsFolder();
    }

    [ExportMethod]
    public void GenerateAlarms() {
        var startingNode = Project.Current.Get<Folder>("CommDrivers");
        UtilsAlarms utilsAlarms = new UtilsAlarms(LogicObject);

        utilsAlarms.GenerateDigitalAlamrsFromCommTags(startingNode.NodeId);
    }
}
