#region Using directives
using System;
using System.Text;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.Core;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FTOptix.CommunicationDriver;
using FTOptix.S7TCP;
using FTOptix.S7TiaProfinet;
using FTOptix.Alarm;
using FTOptix.CODESYS;
using utilx.Utils;
#endregion

public class ImportExportTags : BaseNetLogic
{
    [ExportMethod]
    public void ExportToCsv()
    {
        var startingNode = InformationModel.Get(LogicObject.GetVariable("StartingNodeToFetch").Value);
        UtilsTags utilsTags = new UtilsTags(LogicObject, startingNode);
        utilsTags.ExportToCsv();
    }

    [ExportMethod]
    public void ImportOrUpdateFromCsv()
    {
        var startingNode = InformationModel.Get(LogicObject.GetVariable("StartingNodeToFetch").Value);
        UtilsTags utilsTags = new UtilsTags(LogicObject, startingNode);
        utilsTags.ImportOrUpdateFromCsv();
    }
}
