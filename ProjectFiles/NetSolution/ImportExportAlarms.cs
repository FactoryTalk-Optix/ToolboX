#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.CODESYS;
using FTOptix.S7TiaProfinet;
using FTOptix.NetLogic;
using FTOptix.NativeUI;
using FTOptix.UI;
using FTOptix.CommunicationDriver;
using FTOptix.Core;
using FTOptix.CoreBase;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using FTOptix.Alarm;
using System.Text.RegularExpressions;
using System.Text;
using FTOptix.DataLogger;
#endregion

public class ImportExportAlarms : BaseNetLogic
{
[ExportMethod]
    public void ImportAlarms()
    {
        List<string> commonProperty = new List<string>() {"Enabled","AutoAcknowledge","AutoConfirm","Severity","Message","HighHighLimit","HighLimit","LowLowLimit","LowLimit","LastEvent","InputValue", "NormalStateValue","Setpoint", "PollingTime"};
        var folderPath = GetCSVFilePath();
        if (string.IsNullOrEmpty(folderPath))
        {
            Log.Error("AlarmImportAndExport", "No CSV file chosen, please fill the CSVPath variable");
            return;
        }

        char fieldDelimiter = (char)GetFieldDelimiter();
        if (fieldDelimiter == '\0')
            return;

        bool wrapFields = GetWrapFields();

        foreach (string file in Directory.EnumerateFiles(folderPath, "*.csv"))
        {
            try
            {
                using (CSVFileReader reader = new CSVFileReader(file) { FieldDelimiter = fieldDelimiter, WrapFields = wrapFields })
                {
                    List<CsvUaObject> csvUaObjects = new List<CsvUaObject>();

                    List<string> headerColumns = reader.ReadLine();

                    while (!reader.EndOfFile())
                    {
                        CsvUaObject obj = GetDataFromCsvRow(reader.ReadLine(), headerColumns);
                        // if no data is read from csv, exit from while
                        if (obj == null) continue;
                        csvUaObjects.Add(obj);
                    }

                    if (csvUaObjects.Count == 0)
                    {
                        Log.Error(MethodBase.GetCurrentMethod().Name, file + ":" + $"No valid objects to import.");
                        continue;
                    }

                    List<string> objectTypesIntoFile = csvUaObjects.Select(o => o.TypeBrowsePath).Distinct().ToList();

                    if (objectTypesIntoFile.Count > 1)
                    {
                        Log.Error(MethodBase.GetCurrentMethod().Name, $"Csv file contains data of more than one object type: {string.Join(",", objectTypesIntoFile)}. Aborting.");
                        return;
                    }
                    // Get the name of alarm type by removing all path
                    string csvObjectsCommonType = objectTypesIntoFile.FirstOrDefault().Split('/').LastOrDefault();
                    // Get the node of the alarm type
                    IUANode objectType = Project.Current.Find(csvObjectsCommonType);

                    foreach (CsvUaObject item in csvUaObjects)
                    {
                        CreateFoldersTreeFromPath(item.BrowsePath);
                        Project.Current.Get(item.BrowsePath).Children.Remove(item.Name);
                        IUAObject myNewAlarm = null;
                        if (objectType == null && csvObjectsCommonType.Contains("Controller"))
                        {
                            string s = csvObjectsCommonType.Replace("\"", "");
                            switch (s)
                            {
                                case "OffNormalAlarmController":
                                    myNewAlarm = InformationModel.MakeObject<DigitalAlarm>(item.Name);
                                    break;
                                case "ExclusiveLevelAlarmController":
                                    myNewAlarm = InformationModel.MakeObject<ExclusiveLevelAlarmController>(item.Name);
                                    break;
                                case "NonExclusiveLevelAlarmController":
                                    myNewAlarm = InformationModel.MakeObject<NonExclusiveLevelAlarmController>(item.Name);
                                    break;
                                case "ExclusiveDeviationAlarmController":
                                    myNewAlarm = InformationModel.MakeObject<ExclusiveDeviationAlarmController>(item.Name);
                                    break;
                                case "NonExclusiveDeviationAlarmController":
                                    myNewAlarm = InformationModel.MakeObject<NonExclusiveDeviationAlarmController>(item.Name);
                                    break;
                                case "ExclusiveRateOfChangeAlarmController":
                                    myNewAlarm = InformationModel.MakeObject<ExclusiveRateOfChangeAlarmController>(item.Name);
                                    break;
                                case "NonExclusiveRateOfChangeAlarmController":
                                    myNewAlarm = InformationModel.MakeObject<NonExclusiveRateOfChangeAlarmController>(item.Name);
                                    break;
                                default:
                                    break;
                            }
                        }
                        else if (objectType != null)
                        {
                            // Check if object get from name is an Alarm
                            if (!objectType.GetType().Namespace.Contains("Alarm"))
                            {
                                // if not contain the namespace try to find from the full path declared into the csv
                                objectType = Project.Current.Get(objectTypesIntoFile.FirstOrDefault());
                                if (objectType==null)
                                {
                                    Log.Error(MethodBase.GetCurrentMethod().Name, $"Object Type {objectTypesIntoFile.FirstOrDefault()} does not exist into the Uniqo project");
                                    continue;
                                }
                            }
                            myNewAlarm = InformationModel.MakeObject(item.Name, objectType.NodeId);
                        }
                        else
                        {
                            Log.Error(MethodBase.GetCurrentMethod().Name, $"Object Type {csvObjectsCommonType} does not exist into the Uniqo project");
                            continue;
                        }
                         // Check all common properties
                        foreach (string commonPrp in commonProperty)
                        {
                            // Messagge property have an additional check to perform
                            if (commonPrp == "Message")
                            {
                                string message = item.Variables.SingleOrDefault(v => v.Key == commonPrp).Value;
                                // Interpret the message field read by the current alarm as TextID if MessageAsTranslationKey is set to true and
                                // perform a lookup in the translation table
                                if (GetMessageAsTranslationKey())
                                {
                                    LocalizedText localizedMessage = new LocalizedText(message);
                                    if (!InformationModel.LookupTranslation(localizedMessage).HasTranslation)
                                    {
                                        Log.Warning("AlarmImportAndExport", $"Alarm {myNewAlarm.BrowseName} Message with translation key \"{message}\" was not found (MessageAsTranslationKey = true)");
                                        return;
                                    }
                                    ((AlarmController)myNewAlarm).LocalizedMessage = localizedMessage;
                                }
                                else if (!string.IsNullOrEmpty(message))
                                {
                                    ((AlarmController)myNewAlarm).Message = message;
                                }
                            }
                            else
                                // Try setting the value read in the csv file
                                TrySetOptinalProperty((AlarmController)myNewAlarm, commonPrp, item.Variables.SingleOrDefault(v => v.Key == commonPrp).Value);
                        }                     
                        // Get all uncommon properties of the alarm to set its the value
                        foreach (var property in myNewAlarm.Children.Where(x => !commonProperty.Contains(x.BrowseName)))
                        {                             
                            string valProp = item.Variables.SingleOrDefault(v => v.Key == property.BrowseName).Value;
                            // first set value as plain text
                            myNewAlarm.GetVariable(property.BrowseName).Value = valProp;
                            // Try to manage the value as dynamic link.
                            SetValueProperty(myNewAlarm.GetVariable(property.BrowseName), property.BrowseName, valProp);                                        
                        }
                        Project.Current.Get(item.BrowsePath).Children.Add(myNewAlarm);
                    }
                }
                Log.Info("AlarmImporter", "Alarms successfully imported from " + file);
            }
            catch (Exception ex)
            {
                Log.Error("AlarmImporter", "Unable to import alarms from " + file + ": " + ex.ToString());
            }
        }

        
    }

    private void TrySetOptinalProperty(AlarmController alarm,string propertyName, string propertyValue)
    {
        // If property is null or empty, exit from void
        if (propertyValue == "" || propertyValue == null)
            return;
        var test = alarm.GetOptionalVariableValue(propertyName);
        if (test != null)
        {
            // Switch between the type of the property value
            switch (test.Value)
            {
                case bool boolVal:
                    alarm.SetOptionalVariableValue(propertyName, ConvertStringToBool(propertyValue));
                    break;
                case int intVal:
                    if (!int.TryParse(propertyValue, out int intValue))
                        SetValueProperty(alarm.GetOrCreateVariable(propertyName), propertyName,propertyValue);
                    else
                        alarm.SetOptionalVariableValue(propertyName, intValue);
                    break;
                case double doubleVal:
                    if (!double.TryParse(propertyValue, out double doublevalue))
                        SetValueProperty(alarm.GetOrCreateVariable(propertyName), propertyName,propertyValue);
                    else
                        alarm.SetOptionalVariableValue(propertyName, doublevalue);
                    break;
                case float floatVal:
                    if (!float.TryParse(propertyValue, out float floatvalue))
                        SetValueProperty(alarm.GetOrCreateVariable(propertyName), propertyName,propertyValue);
                    else
                        alarm.SetOptionalVariableValue(propertyName, floatvalue);
                    break;
                case ushort ushortVal:
                    if (!ushort.TryParse(propertyValue, out ushort ushortvalue))
                        SetValueProperty(alarm.GetOrCreateVariable(propertyName), propertyName,propertyValue);
                    else
                        alarm.SetOptionalVariableValue(propertyName, ushortvalue);
                    break;
                case uint uintVal:
                    if (!uint.TryParse(propertyValue, out uint uintvalue))
                        SetValueProperty(alarm.GetOrCreateVariable(propertyName), propertyName,propertyValue);
                    else
                        alarm.SetOptionalVariableValue(propertyName, uintvalue);
                    break;
                case ulong ulongVal:
                    if (!ulong.TryParse(propertyValue, out ulong ulongvalue))
                        SetValueProperty(alarm.GetOrCreateVariable(propertyName), propertyName,propertyValue);
                    else
                        alarm.SetOptionalVariableValue(propertyName, ulongvalue);
                    break;
                default:
                    // in case of is an unknow type, try to manage as dynamic link
                    SetValueProperty(alarm.GetOrCreateVariable(propertyName),propertyName,propertyValue);
                    break;
            }
        }
    }
    
    private bool ConvertStringToBool(string value)
    {
        return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private void SetValueProperty(IUAVariable alarmField, string propertyBrowseName, string propertyValue)
    {
        IUAVariable varDynLink;
        Regex findBrac;
        // First check if it is an Alias or a Variable. For Alias check if the string start with a value in angle brackets
        findBrac = new Regex(@"^\{.*?\}");
        if (findBrac.Match(propertyValue).Success)
        {
            // Create a Variable of type DataBin with datatype NodePath
            DynamicLink aliasDataBindVar= InformationModel.MakeVariable<DynamicLink>("AppCrAl_"+propertyBrowseName, FTOptix.Core.DataTypes.NodePath);
            // Set value with the full Alias path {AliasName}/Path1/Path2/../Var
            aliasDataBindVar.Value = propertyValue;
            // Set reference to alarm field with the Databind
            alarmField.Refs.AddReference(FTOptix.CoreBase.ReferenceTypes.HasDynamicLink, aliasDataBindVar);
        }
        else
        {
            // If the string in the csv is an array, I extract the value. Regular expression check squadre brackets with digit values
            findBrac = new Regex(@"\[.*?\d\]");
            if (findBrac.Match(propertyValue).Success)
            {
                string matchRes = findBrac.Match(propertyValue).Value;
                // Variable name is the full keyvalue without the match of regex. try to get from the project
                varDynLink =  Project.Current.GetVariable(propertyValue.Replace(matchRes,""));
                // If the result of GetVariable is null, write a warning and return
                if (varDynLink == null)
                {
                    Log.Warning("AlarmImporter", $"Unable to find the variabile {propertyValue.Replace(matchRes,"")} for the custom alarm property {propertyBrowseName}"); 
                    return;                                  
                }
                if (matchRes.Contains(","))
                {
                    // if is multi-dimensional array, first set the variable
                    alarmField.SetDynamicLink(varDynLink);
                    // next replace the value of dynamic link with the multi dimensional (the regex Match value)
                    alarmField.GetVariable("DynamicLink").Value = alarmField.GetVariable("DynamicLink").Value + matchRes;
                }
                else
                {
                    // if is a single array, extract the index and set the DynamicLink
                    uint indArr = 0;
                    if (uint.TryParse(matchRes.Replace("[","").Replace("]",""), out indArr))
                        alarmField.SetDynamicLink(varDynLink, indArr);
                }                                    
            }
            else
            {                               
                // Try to get the variable from the project
                varDynLink =  Project.Current.GetVariable(propertyValue);
                // If the result of GetVariable is null, write a warning and return
                if (varDynLink == null)
                {
                    Log.Warning("AlarmImporter", $"Unable to find the variabile {propertyValue} for the custom alarm property {propertyBrowseName}");                                   
                    return;
                }
                // Create the dynamic link
                alarmField.SetDynamicLink(varDynLink);
            }               
        }                                                
    }

    private List<IUAObjectType> GetAlarmTypeList()
    {
        var alarms = new List<IUAObjectType>();
        var projectNamespaceIndex = LogicObject.NodeId.NamespaceIndex;
        // Insert code to be executed by the method
        var alamrControllerType = InformationModel.Get(FTOptix.Alarm.ObjectTypes.AlarmController);
        var allControllerTypes = new List<IUAObjectType>();
        CollectRecursive((IUAObjectType)alamrControllerType, allControllerTypes);
        var concreteTypes = allControllerTypes.FindAll(type => !type.IsAbstract);
        //Log.Info("ALL ALARM CONTROLLER TYPE ARE:");
        foreach (var e in concreteTypes)
            alarms.Add(e);
        var userDefinedTypes = concreteTypes.FindAll(type => type.NodeId.NamespaceIndex == projectNamespaceIndex);
        foreach (var e in userDefinedTypes)
        {
            alarms.Add(e);
        }
        return alarms;
    }



    [ExportMethod]
    public void ExportAlarms()
    {
        var csvPath = GetCSVFilePath();
        if (string.IsNullOrEmpty(csvPath))
        {
            Log.Error("AlarmImportAndExport", "No CSV file chosen, please fill the CSVPath variable");
            return;
        }

        char? fieldDelimiter = GetFieldDelimiter();
        if (fieldDelimiter == null || fieldDelimiter == '\0')
            return;

        bool wrapFields = GetWrapFields();

        List<IUAObjectType> typesAlarm = GetAlarmTypeList();

        foreach (var alarmCustomType in typesAlarm)
        {
            string pathalarmType = GetBrowsePathFromIuaNode(InformationModel.Get(alarmCustomType.NodeId));
            List<string> propertiesFields = new List<string>();
            List<string> valuesFields = new List<string>();
            propertiesFields.Add("Name");
            propertiesFields.Add("Type");
            propertiesFields.Add("Path");
            CheckAlarmProperties(alarmCustomType.NodeId, propertiesFields);

            try
            {
                using (var csvWriter = new CSVFileWriter(csvPath + "/" + alarmCustomType.BrowseName + ".csv") { FieldDelimiter = fieldDelimiter.Value, WrapFields = wrapFields })
                {
                    csvWriter.WriteLine(propertiesFields.ToArray());

                    foreach (AlarmController alarm in GetAlarmList(alarmCustomType.NodeId))
                    {
                        var alarmFields = CollectAlarmConfiguration(alarm);

                        valuesFields = new List<string>();
                        valuesFields.Add(alarm.BrowseName);
                        valuesFields.Add(pathalarmType);
                        valuesFields.Add(GetBrowsePathWithoutNodeName(alarm));

                        foreach (var item in propertiesFields)
                        {
                            if (item == "Name" || item == "Type" || item == "Path" || item == "InputValueArrayIndex" || item == "InputValueArraySubIndex")
                                continue;
                            if (item == "InputValue")
                            {                                
                                valuesFields.Add(ExportAlarmVariable(alarm.InputValueVariable));
                            }
                            else if (item == "Message")
                            {
                                if (GetMessageAsTranslationKey())
                                {
                                    // When MessageAsTranslationKey is set to true, we need to export the TextID of Message (not the Message Text)
                                    var localizedTextMessage = ((AlarmController)alarm).LocalizedMessage;
                                    if (localizedTextMessage != null && localizedTextMessage.HasTextId)
                                        valuesFields.Add(localizedTextMessage.TextId);
                                    else
                                    {
                                        Log.Warning("AlarmImportAndExport", $"Message of alarm {alarm.BrowseName} has no translation key. Message of this alarm will not exported (MessageAsTranslationKey = true)");
                                        valuesFields.Add("");
                                    }
                                }
                                else
                                {
                                    // When MessageAsTranslationKey is set to false, we need to export the content of Message
                                    if (alarm.GetVariable("Message") != null)
                                        valuesFields.Add(((AlarmController)alarm).Message);
                                    else
                                        valuesFields.Add("");
                                }
                            }
                            else
                            {
                                if (((AlarmController)alarm).GetVariable(item) != null)
                                    
                                    if (alarm.GetVariable(item).GetVariable("DynamicLink") != null) 
                                    {
                                        // if the field contain a DynamicLink, export the variable path.
                                        valuesFields.Add(ExportAlarmVariable(alarm.GetVariable(item)));
                                    }
                                    else
                                    {
                                        // if not a variable, export the value of the field
                                        valuesFields.Add(((AlarmController)alarm).GetVariable(item).Value);
                                    }
                                else
                                    valuesFields.Add("");
                            }

                        }
                        csvWriter.WriteLine(valuesFields.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("AlarmExporter", "Unable to export alarms: " + ex);
            }
        }
        Log.Info("AlarmExporter", "Alarms successfully exported to " + csvPath);
        return;
    }

    private string GetBrowsePathWithoutNodeName(IUANode uaObj)
    {
        var browsePath = GetBrowsePathFromIuaNode(uaObj);
        return browsePath.Contains("/") ? browsePath.Substring(0, browsePath.LastIndexOf("/", StringComparison.Ordinal)) : browsePath;
    }

    private string GetBrowsePathFromIuaNode(IUANode uaNode) => ClearPathFromProjectInfo(Log.Node(uaNode));
    private string ClearPathFromProjectInfo(string path)
    {
        var projectName = Project.Current.BrowseName + "/";
        var occurrence = path.IndexOf(projectName);
        if (occurrence == -1) { return path; }

        path = path.Substring(occurrence + projectName.Length);
        return path;
    }

    private string ExportAlarmVariable(IUAVariable varToAnalyze)
    {
        string pathToInputValueNode ="";
        // Get the DataBind (variable linked) of the Dynamic Link
        DynamicLink inputPath = (DynamicLink)varToAnalyze.GetVariable("DynamicLink");
        // If inputPath is null, return empty string
        if (inputPath == null) return "";
        // Resolve the path of variable linked to the field
        PathResolverResult resolvePathResult = LogicObject.Context.ResolvePath(varToAnalyze, inputPath.Value);
        // If resolvePathResult is null, return empty string
        if (resolvePathResult == null) return "";
        // Check if is an Alias or Variable
        if (resolvePathResult.AliasSpecification != null && resolvePathResult.AliasSpecification.AliasTokenPath != "")
        {
            // If is alias return the full value of inputPath like {aliasName}\<struct>
            pathToInputValueNode = inputPath.Value;
        }
        else
        {
            // Get the path in string format of the variable for write to CSV file
            pathToInputValueNode = MakeBrowsePath(resolvePathResult.ResolvedNode);
            // if the Indexes is plus then 0, mean is an array (more indexex, more dimension of array)       
            if (resolvePathResult.Indexes != null && resolvePathResult.Indexes.Length > 0)
            {
                // Open square brackets for string notation 
                pathToInputValueNode += "[";
                // for each index append the value on the string with a , as separator
                for (int i=0; i < resolvePathResult.Indexes.Length; i++)
                {
                    pathToInputValueNode += resolvePathResult.Indexes[i];
                    // if not the last element add a ,
                    if (i != resolvePathResult.Indexes.Length -1) pathToInputValueNode+=",";
                
                }
                // Close the square brackets for string notation
                pathToInputValueNode +="]";
            }
        }       
        return pathToInputValueNode;
    }
    
    private string MakeBrowsePath(IUANode node)
    {
        string path = node.BrowseName;
        var current = node.Owner;

        while (current != Project.Current)
        {
            path = current.BrowseName + "/" + path;
            current = current.Owner;
        }
        return path;
    }
    private List<string> CollectAlarmConfiguration(IUAObject alarm)
    {
        var alarmFields = new List<string>();

        foreach (var item in alarm.Children)
        {
            alarmFields.Add(item.BrowseName);
        }

        return alarmFields;
    }

    private List<IUAObject> GetAlarmList(NodeId alarmTypeNodeId)
    {
        var alarms = new List<IUAObject>();
        var typedAlarms = GetAlarmsByType(alarmTypeNodeId);
        foreach (var typedAlarm in typedAlarms)
            alarms.Add(typedAlarm);

        return alarms;
    }

    private IReadOnlyList<IUAObject> GetAlarmsByType(NodeId type)
    {
        var alarmType = LogicObject.Context.GetObjectType(type);
        var alarms = alarmType.InverseRefs.GetObjects(OpcUa.ReferenceTypes.HasTypeDefinition, false);
        return alarms;
    }

    private bool GetMessageAsTranslationKey()
    {
        var messageAsTranslationKeyVariable = LogicObject.GetVariable("MessageAsTranslationKey");
        return messageAsTranslationKeyVariable == null ? false : (bool)messageAsTranslationKeyVariable.Value;
    }

    private string GetCSVFilePath()
    {
        var csvPathVariable = LogicObject.Children.Get<IUAVariable>("CSVPath");
        if (csvPathVariable == null)
        {
            Log.Error("AlarmImportAndExport", "CSVPath variable not found");
            return "";
        }

        return new ResourceUri(csvPathVariable.Value).Uri;
    }

    private char? GetFieldDelimiter()
    {
        var separatorVariable = LogicObject.GetVariable("CharacterSeparator");
        if (separatorVariable == null)
        {
            Log.Error("AlarmImportAndExport", "CharacterSeparator variable not found");
            return null;
        }

        string separator = separatorVariable.Value;

        if (separator.Length != 1 || separator == String.Empty)
        {
            Log.Error("AlarmImportAndExport", "Wrong CharacterSeparator configuration. Please insert a char");
            return null;
        }

        if (char.TryParse(separator, out char result))
            return result;

        return null;
    }

    private bool GetWrapFields()
    {
        var wrapFieldsVariable = LogicObject.GetVariable("WrapFields");
        if (wrapFieldsVariable == null)
        {
            Log.Error("AlarmImportAndExport", "WrapFields variable not found");
            return false;
        }

        return wrapFieldsVariable.Value;
    }

    private NodeId GetAlarmType()
    {
        var alarmType = LogicObject.GetVariable("AlarmType");
        if (alarmType.Value.Value == null)
        {
            Log.Error("AlarmImportAndExport", "AlarmType variable not found");
            return null;
        }
        return InformationModel.Get(alarmType.Value).NodeId;
    }

    private static bool CreateFoldersTreeFromPath(string path)
    {
        
        if (string.IsNullOrEmpty(path)) { return true; }
        var segments = path.Split('/').ToList();
        string updatedSegment = "";
        string segmentsAccumulator = "";

        try
        {
            foreach (var s in segments)
            {
                if (segmentsAccumulator == "")
                    updatedSegment = s;
                else
                    updatedSegment = updatedSegment + "/" + s;
                var folder = InformationModel.MakeObject<Folder>(s);
                var folderAlreadyExists = Project.Current.GetObject(updatedSegment) != null;
                if (!folderAlreadyExists) {
                    if (segmentsAccumulator == "")
                        Project.Current.Add(folder);
                    else
                        Project.Current.GetObject(segmentsAccumulator).Children.Add(folder); 
                }
                segmentsAccumulator = updatedSegment;
            }
        }
        catch (Exception e)
        {
            Log.Error(MethodBase.GetCurrentMethod().Name, $"Cannot create folder, error {e.Message}");
            return false;
        }
        return true;
    }

    private CsvUaObject GetDataFromCsvRow(List<string> line, List<string> header)
    {
        var csvUaObject = new CsvUaObject
        {
            Name = line[0],
            TypeBrowsePath = line[1],
            BrowsePath = line[2]
        };

        if (!csvUaObject.IsValid())
        {
            Log.Error(MethodBase.GetCurrentMethod().Name, $"Invalid object with name {csvUaObject.Name}. Please check its properties.");
            return null;
        }

        for (var i = 3; i < header.Count; i++)
        {
            csvUaObject.Variables.Add(header[i], line[i]);
        }

        return csvUaObject;
    }

    private class CsvUaObject
    {
        private const string CSV_NAME_COLUMN = "Name";
        private const string CSV_TYPE_COLUMN = "Type";
        private const string CSV_PATH_COLUMN = "Path";
        public const string CSV_INPUT_VALUE_PATH_COLUMN = "InputValuePath";
        public const string CSV_INPUT_VALUE_COLUMN = "InputValue";
        private static readonly string[] CSV_VARIABLES_STARTING_HEADER = new string[] { CSV_NAME_COLUMN, CSV_TYPE_COLUMN, CSV_PATH_COLUMN };

        public string Name { get; set; }
        public string TypeBrowsePath { get; set; }
        public string BrowsePath { get; set; }
        public Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(TypeBrowsePath)
                    && !string.IsNullOrWhiteSpace(Name)
                        && !string.IsNullOrWhiteSpace(BrowsePath);
        }

        public static string[] GetCsvFixedHeaderColumns() => CSV_VARIABLES_STARTING_HEADER;

        internal static void WriteToCsv(List<CsvUaObject> csvUaObjects, List<string> csvColumnsNames, CSVFileWriter csvWriter)
        {
            foreach (var o in csvUaObjects)
            {
                if (!o.IsValid()) { Log.Error(MethodBase.GetCurrentMethod().Name, $"Cannot export object {o.Name}: not Valid"); }
                var csvRow = new List<string>() { o.Name, o.TypeBrowsePath, o.BrowsePath };

                foreach (var column in csvColumnsNames)
                {
                    var objVariable = o.Variables.SingleOrDefault(v => v.Key == column);
                    if (objVariable.Equals(new KeyValuePair<string, string>()))
                    {
                        csvRow.Add(string.Empty);
                        continue;
                    }
                    csvRow.Add(objVariable.Value);
                }

                try
                {
                    csvWriter.WriteLine(csvRow.ToArray());
                }
                catch (Exception e)
                {
                    Log.Error(MethodBase.GetCurrentMethod().Name, $"Cannot export object {o.Name}, error: {e.Message}");
                }
            }
        }
    }
    private class CSVFileReader : IDisposable
    {
        public char FieldDelimiter { get; set; } = ',';

        public char QuoteChar { get; set; } = '"';

        public bool WrapFields { get; set; } = false;

        public bool IgnoreMalformedLines { get; set; } = false;

        public CSVFileReader(string filePath, System.Text.Encoding encoding)
        {
            streamReader = new StreamReader(filePath, encoding);
        }

        public CSVFileReader(string filePath)
        {
            streamReader = new StreamReader(filePath, System.Text.Encoding.UTF8);
        }

        public CSVFileReader(StreamReader streamReader)
        {
            this.streamReader = streamReader;
        }

        public bool EndOfFile()
        {
            return streamReader.EndOfStream;
        }

        public List<string> ReadLine()
        {
            if (EndOfFile())
                return null;

            var line = streamReader.ReadLine();

            var result = WrapFields ? ParseLineWrappingFields(line) : ParseLineWithoutWrappingFields(line);

            currentLineNumber++;
            return result;

        }

        public List<List<string>> ReadAll()
        {
            var result = new List<List<string>>();
            while (!EndOfFile())
                result.Add(ReadLine());

            return result;
        }

        private List<string> ParseLineWithoutWrappingFields(string line)
        {
            if (string.IsNullOrEmpty(line) && !IgnoreMalformedLines)
                throw new FormatException($"Error processing line {currentLineNumber}. Line cannot be empty");

            return line.Split(FieldDelimiter).ToList();
        }

        private List<string> ParseLineWrappingFields(string line)
        {
            var fields = new List<string>();
            var buffer = new StringBuilder("");
            var fieldParsing = false;

            int i = 0;
            while (i < line.Length)
            {
                if (!fieldParsing)
                {
                    if (IsWhiteSpace(line, i))
                    {
                        ++i;
                        continue;
                    }

                    // Line and column numbers must be 1-based for messages to user
                    var lineErrorMessage = $"Error processing line {currentLineNumber}";
                    if (i == 0)
                    {
                        // A line must begin with the quotation mark
                        if (!IsQuoteChar(line, i))
                        {
                            if (IgnoreMalformedLines)
                                return null;
                            else
                                throw new FormatException($"{lineErrorMessage}. Expected quotation marks at column {i + 1}");
                        }

                        fieldParsing = true;
                    }
                    else
                    {
                        if (IsQuoteChar(line, i))
                            fieldParsing = true;
                        else if (!IsFieldDelimiter(line, i))
                        {
                            if (IgnoreMalformedLines)
                                return null;
                            else
                                throw new FormatException($"{lineErrorMessage}. Wrong field delimiter at column {i + 1}");
                        }
                    }

                    ++i;
                }
                else
                {
                    if (IsEscapedQuoteChar(line, i))
                    {
                        i += 2;
                        buffer.Append(QuoteChar);
                    }
                    else if (IsQuoteChar(line, i))
                    {
                        fields.Add(buffer.ToString());
                        buffer.Clear();
                        fieldParsing = false;
                        ++i;
                    }
                    else
                    {
                        buffer.Append(line[i]);
                        ++i;
                    }
                }
            }

            return fields;
        }

        private bool IsEscapedQuoteChar(string line, int i)
        {
            return line[i] == QuoteChar && i != line.Length - 1 && line[i + 1] == QuoteChar;
        }

        private bool IsQuoteChar(string line, int i)
        {
            return line[i] == QuoteChar;
        }

        private bool IsFieldDelimiter(string line, int i)
        {
            return line[i] == FieldDelimiter;
        }

        private bool IsWhiteSpace(string line, int i)
        {
            return Char.IsWhiteSpace(line[i]);
        }

        private StreamReader streamReader;
        private int currentLineNumber = 1;

        #region IDisposable support
        private bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
                streamReader.Dispose();

            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }

    private class CSVFileWriter : IDisposable
    {
        public char FieldDelimiter { get; set; } = ',';

        public char QuoteChar { get; set; } = '"';

        public bool WrapFields { get; set; } = false;

        public CSVFileWriter(string filePath)
        {
            streamWriter = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
        }

        public CSVFileWriter(string filePath, System.Text.Encoding encoding)
        {
            streamWriter = new StreamWriter(filePath, false, encoding);
        }

        public CSVFileWriter(StreamWriter streamWriter)
        {
            this.streamWriter = streamWriter;
        }

        public void WriteLine(string[] fields)
        {
            var stringBuilder = new StringBuilder();

            for (var i = 0; i < fields.Length; ++i)
            {
                if (WrapFields)
                    stringBuilder.AppendFormat("{0}{1}{0}", QuoteChar, EscapeField(fields[i]));
                else
                    stringBuilder.AppendFormat("{0}", fields[i]);

                if (i != fields.Length - 1)
                    stringBuilder.Append(FieldDelimiter);
            }

            streamWriter.WriteLine(stringBuilder.ToString());
            streamWriter.Flush();
        }

        private string EscapeField(string field)
        {
            var quoteCharString = QuoteChar.ToString();
            return field.Replace(quoteCharString, quoteCharString + quoteCharString);
        }

        private StreamWriter streamWriter;

        #region IDisposable Support
        private bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
                streamWriter.Dispose();

            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }

    private void CheckAlarmProperties(NodeId alarmType, List<string> propertyList)
    {
        List<string> commonProperty = new List<string>() { "Enabled", "AutoAcknowledge", "AutoConfirm", "Severity", "Message", "HighHighLimit", "HighLimit", "LowLowLimit", "LowLimit", "InputValue", "InputValueArrayIndex", "InputValueArraySubIndex", "NormalStateValue" };

        IUANode myAlarm = InformationModel.Get(alarmType);
        IUAObjectType myAlarmSuperType = ((UAObjectType)myAlarm).SuperType;
        
        while (myAlarmSuperType != null)
        {
            if (myAlarmSuperType.BrowseName == "AlarmController" || myAlarmSuperType.BrowseName == "LimitAlarmController")
            {
                foreach (var item in myAlarmSuperType.Children)
                {
                    if (commonProperty.Contains(item.BrowseName))
                    {
                        propertyList.Add(item.BrowseName);                            
                    }                       
                }
            }
            myAlarmSuperType = myAlarmSuperType.SuperType;
        }

        foreach (var item in InformationModel.Get(alarmType).Children)
        {
            if (propertyList.Contains(item.BrowseName) || item.BrowseName == "LastEvent")
                continue;
            propertyList.Add(item.BrowseName);
        }

    }

    void CollectRecursive(IUAObjectType parentType, List<IUAObjectType> allControllerTypes)
    {
        allControllerTypes.Add(parentType);
        foreach (var childType in parentType.Refs.GetObjectTypes(OpcUa.ReferenceTypes.HasSubtype, false))
            CollectRecursive(childType, allControllerTypes);
    }
}
