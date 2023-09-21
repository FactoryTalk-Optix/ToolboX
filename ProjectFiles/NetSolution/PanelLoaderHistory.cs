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
using FTOptix.Retentivity;
using FTOptix.CommunicationDriver;
using FTOptix.Core;
using utilx.Utils;
using FTOptix.CODESYS;
#endregion

public class PanelLoaderHistory : BaseNetLogic
{
    private PanelLoaderHistoryManager panelLoaderHistoryManager;

    public override void Start()
    {
        panelLoaderHistoryManager = UtilsScreens.CreatePanelLoaderHistoryManager(Owner.NodeId);
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }

    [ExportMethod]
    public void Next()
    {
        panelLoaderHistoryManager.HistoryForward();
    }

    [ExportMethod]
    public void Previous()
    {
        panelLoaderHistoryManager.HistoryBack();
    }

    [ExportMethod]
    public void ClearHistory()
    {
        panelLoaderHistoryManager.ClearHistory();
    }

}
