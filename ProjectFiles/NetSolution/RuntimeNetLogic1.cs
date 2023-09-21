#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.Recipe;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.NativeUI;
using FTOptix.UI;
using FTOptix.CoreBase;
using FTOptix.SQLiteStore;
using FTOptix.Store;
using FTOptix.Modbus;
using FTOptix.S7TCP;
using FTOptix.CODESYS;
using FTOptix.Retentivity;
using FTOptix.CommunicationDriver;
using FTOptix.Core;
using utilx.Utils;
using FTOptix.DataLogger;
#endregion

public class RuntimeNetLogic1 : BaseNetLogic
{
    private UtilsStore utilsStore;

    public override void Start()
    {
        var store = Project.Current.Get<Store>("DataStores/EmbeddedDatabase1");
        utilsStore = new UtilsStore(LogicObject, store);
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    [ExportMethod]
    public void GenerateData()
    {
        utilsStore.PopulateTableWithRandomData("RecipeSchema1", 100);
    }

    [ExportMethod]
    public void DeleteData() {
        utilsStore.TruncateTableData("RecipeSchema1");
    }
}
