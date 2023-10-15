#region Using directives
using System;
using UAManagedCore;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.CoreBase;
using FTOptix.Core;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Globalization;
using FTOptix.Alarm;
using FTOptix.CODESYS;
using utilx.Utils;
using FTOptix.DataLogger;
using FTOptix.S7TiaProfinet;
#endregion

public class ImportExportModel : BaseNetLogic
{
    [ExportMethod]
    public void ExportModelToCsv()
    {
        var startingNode = InformationModel.Get(LogicObject.GetVariable("StartingNodeToFetch").Value);
        UtilsModel utilsModel = new UtilsModel(LogicObject, startingNode);
        utilsModel.ExportModelToCsv();
    }

    [ExportMethod]
    public void ImportModelFromCsv()
    {
        var startingNode = InformationModel.Get(LogicObject.GetVariable("StartingNodeToFetch").Value);
        UtilsModel utilsModel = new UtilsModel(LogicObject, startingNode);
        utilsModel.ImportModelFromCsv();
    }
}
