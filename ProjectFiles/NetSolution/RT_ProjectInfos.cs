#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.Modbus;
using FTOptix.NativeUI;
using FTOptix.UI;
using FTOptix.Alarm;
using FTOptix.S7TCP;
using FTOptix.Retentivity;
using FTOptix.CoreBase;
using FTOptix.CommunicationDriver;
using FTOptix.Core;
using utilx.Utils;
using FTOptix.Recipe;
using FTOptix.SQLiteStore;
using FTOptix.Store;
using FTOptix.EventLogger;
using FTOptix.CODESYS;
#endregion

public class RT_ProjectInfos : BaseNetLogic
{
    private UtilsProjectInformations utilsProjectInformations;
    private UtilsRecipes utilsRecipes;

    public override void Start()
    {
        utilsProjectInformations = new UtilsProjectInformations(LogicObject);
        utilsRecipes = new UtilsRecipes(LogicObject);
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    [ExportMethod]
    public void GetProjectInfos()
    {
        utilsProjectInformations.GetProjectInfos();
    }

    [ExportMethod]
    public void ResetEditModel(NodeId recipeSchemaNodeId)
    {
        utilsRecipes.SetDefaultsToEditModelVariables(recipeSchemaNodeId);
    }

    [ExportMethod]
    public void GetNodesNumber(NodeId screenNodeId) {
        Log.Info("Screen nodes number: " + UtilsProjectInformations.GetNodesNumber(screenNodeId));
    }
}
