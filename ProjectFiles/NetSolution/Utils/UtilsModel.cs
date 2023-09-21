using FTOptix.Core;
using FTOptix.CoreBase;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UAManagedCore;

namespace utilx.Utils
{
    internal class UtilsModel
    {
        private readonly IUAObject _logicObject;
        private string _modelCsvUri;
        private IUANode _startingNode;
        private const string _csvSeparator = ";";
        private const string _arraySeparator = ",";
        private const string _CSV_FILENAME = "model.csv";
        private readonly List<string> _csvHeaderElements = new List<string> {
        TYPE_ATTRIBUTE
        , BROWSENAME_ATTRIBUTE
        , BROWSEPATH_ATTRIBUTE
        , DATATYPE_ATTRIBUTE
        , ARRAYLENGTH_ATTRIBUTE
        , DYNAMICLINK_ATTRIBUTE
        , VARIABLE_VALUE
        , CUSTOM_TYPE
        };

        private const string TYPE_ATTRIBUTE = "Type";
        private const string BROWSENAME_ATTRIBUTE = "BrowseName";
        private const string BROWSEPATH_ATTRIBUTE = "BrowsePath";
        private const string DATATYPE_ATTRIBUTE = "NodeDataType";
        private const string ARRAYLENGTH_ATTRIBUTE = "ArrayLength";
        private const string DYNAMICLINK_ATTRIBUTE = "DynamicLink";
        private const string VARIABLE_VALUE = "Value";
        private const string CUSTOM_TYPE = "CustomType";

        public UtilsModel(IUAObject logicObject, IUANode startingNode)
        {
            _logicObject = logicObject;
            _modelCsvUri = ResourceUri.FromProjectRelativePath(_CSV_FILENAME).Uri;
            _startingNode = startingNode;
        }

        /// <summary>
        /// Exports variables and objects starting form /Model or a specific node
        /// </summary>
        public void ExportModelToCsv()
        {
            WriteModelToCsv(_startingNode);
        }

        public void ImportModelFromCsv()
        {
            UpdateModelFromCsv();
        }

        #region private methods

        private void UpdateModelFromCsv()
        {
             _startingNode.Children.Clear(); 
            using (StreamReader sReader = new StreamReader(_modelCsvUri))
            {
                var header = sReader.ReadLine();
                while (!sReader.EndOfStream)
                {
                    var line = sReader.ReadLine();
                    CreateOrUpdateNodeFromCsvLine(line.Split(_csvSeparator), header.Split(_csvSeparator));
                }
            }

            Log.Info("Import completed.");
        }

        private IUANode CreateOrUpdateNodeFromCsvLine(string[] nodeData, string[] header)
        {
            try
            {
                var type = nodeData[Array.IndexOf(header, TYPE_ATTRIBUTE)];
                var browseName = nodeData[Array.IndexOf(header, BROWSENAME_ATTRIBUTE)];
                var browsePath = nodeData[Array.IndexOf(header, BROWSEPATH_ATTRIBUTE)];
                var dataType = nodeData[Array.IndexOf(header, DATATYPE_ATTRIBUTE)];
                var arrayLength = nodeData[Array.IndexOf(header, ARRAYLENGTH_ATTRIBUTE)];
                var dynamicLink = nodeData[Array.IndexOf(header, DYNAMICLINK_ATTRIBUTE)];
                var value = nodeData[Array.IndexOf(header, VARIABLE_VALUE)];

                var isUAObject = type == typeof(UAManagedCore.UAObject).Name;
                var isFolder = type == typeof(FTOptix.Core.Folder).Name;
                var isUAVariable = type == typeof(UAManagedCore.UAVariable).Name;

                var nodeOwner = GetOwnerNode(_startingNode, browsePath);

                if (isUAObject)
                {
                    var iuaObj = InformationModel.MakeObject(browseName);
                    var existingNode = NodeAlreadyExists(nodeOwner, iuaObj);

                    if (existingNode == null)
                    {
                        nodeOwner.Add(iuaObj);
                    }
                }
                else if (isFolder)
                {
                    var folder = InformationModel.Make<Folder>(browseName);
                    var existingNode = NodeAlreadyExists(nodeOwner, folder);

                    if (existingNode == null)
                    {
                        nodeOwner.Add(folder);
                    }
                }
                else if (isUAVariable)
                {
                    var uaVarDataType = GetOpcUaDataTypeFromStringOpcUaDataType(dataType);
                    IUAVariable uaVar = null;

                    if (arrayLength != string.Empty)
                    {
                        var isMatrix = arrayLength.Contains(_arraySeparator);
                        if (isMatrix)
                        {
                            var indexes = arrayLength.Split(_arraySeparator);
                            var index0 = uint.Parse(indexes[0]);
                            var index1 = uint.Parse(indexes[1]);
                            uaVar = InformationModel.MakeVariable(browseName, uaVarDataType, new uint[2]);
                            uaVar.ArrayDimensions = new uint[] { index0, index1 };
                        }
                        else
                        {
                            uaVar = InformationModel.MakeVariable(browseName, uaVarDataType, new uint[1]);
                            var index = uint.Parse(arrayLength);
                            uaVar.ArrayDimensions = new uint[] { index };
                        }

                    }
                    else
                    {
                        uaVar = InformationModel.MakeVariable(browseName, uaVarDataType);
                    }

                    var hasDynamicLink = dynamicLink != string.Empty;
                    if (hasDynamicLink)
                    {
                        SetDynamicLinkOnVariable(uaVar, dynamicLink);
                    }
                    else
                    {
                        UpdateTagValue(uaVar, value, uaVarDataType);
                    }

                    var existingNode = NodeAlreadyExists(nodeOwner, uaVar);

                    if (existingNode == null)
                    {
                        nodeOwner.Add(uaVar);
                    }
                    else
                    {
                        UpdateVariable((IUAVariable)existingNode, uaVar);
                    }
                }
                else
                {
                    Log.Warning("Type: " + type + " browsename: " + browseName + " is not managed by script");
                }

                return null;
            }
            catch (System.Exception ex)
            {
                Log.Error(MethodBase.GetCurrentMethod().Name + " " + nodeData[Array.IndexOf(header, BROWSENAME_ATTRIBUTE)], ex.Message);
                return null;
            }
        }

        private void UpdateVariable(IUAVariable destinationVar, IUAVariable sourceVar)
        {
            destinationVar.GetType().GetProperty("DataType").SetValue(destinationVar, sourceVar.DataType);

            var dl = sourceVar.GetVariable("DynamicLink");
            if (dl != null)
            {
                SetDynamicLinkOnVariable(destinationVar, dl.Value);
            }
            else
            {
                destinationVar.ResetDynamicLink();
                UpdateTagValue(destinationVar, sourceVar.Value, sourceVar.DataType);
            }
        }

        private static void SetDynamicLinkOnVariable(IUAVariable destinationVar, UAValue dlValue)
        {
            var fakeVar = InformationModel.MakeVariable("fake", UAManagedCore.OpcUa.DataTypes.Boolean);
            destinationVar.SetDynamicLink(fakeVar, DynamicLinkMode.ReadWrite);
            destinationVar.GetVariable("DynamicLink").Value = dlValue;
        }

        private void WriteModelToCsv(IUANode startingNode)
        {
            File.Create(_modelCsvUri).Close();
            var csvHeader = GenerateCsvHeader();
            var nodesToExport = GetNodesToExport(startingNode);
            var joinedData = new List<string>();

            joinedData.AddRange(nodesToExport.Item1.Select(i => CreateCsvRow(i)));
            joinedData.AddRange(nodesToExport.Item2.Select(i => CreateCsvRow(i)));
            joinedData.AddRange(nodesToExport.Item3.Select(i => CreateCsvRow(i)));

            var orderedRows = joinedData.OrderBy(r => GetCsvRowThirdElement(r)).ToList();

            Encoding encoding = Encoding.Unicode;

            using (StreamWriter sWriter = new StreamWriter(_modelCsvUri, false, encoding))
            {
                sWriter.WriteLine(csvHeader);
                foreach (var row in orderedRows)
                {
                    sWriter.WriteLine(row);
                }
            }
            Log.Info("Objects types exported: " + nodesToExport.Item1.Count + " Objects exported: " + nodesToExport.Item2.Count + "; Variables exported: " + nodesToExport.Item3.Count);
        }

        private static string GetCsvRowThirdElement(string row) => row.Split(_csvSeparator)[2].Trim();

        private static string GetObjectCustomeTypeBrowseName(IUANode item) => ((UAManagedCore.UANode)((UAManagedCore.UAObject)item).ObjectType).BrowseName;

        private bool IsCustomObjectTypeInstance(IUANode item) =>
                item is not IUAObjectType
                && item is not Folder
                && item is not IUAVariable
                && !new[] { string.Empty, "BaseObjectType" }.Contains(GetObjectCustomeTypeBrowseName(item));


        private string CreateCsvRow(IUANode item)
        {
            try
            {
                var type = item.GetType().Name;
                var browseName = item.BrowseName;
                var browsePath = GetBrowsePath(_startingNode, item);

                var isUAVariable = item is IUAVariable;
                var isCustomObjectTypeInstance = IsCustomObjectTypeInstance(item);

                var dataType = isUAVariable ?
                                            InformationModel.Get(((IUAVariable)item).DataType).BrowseName
                                            : string.Empty;
                var isArray = !isUAVariable || ((IUAVariable)item).ArrayDimensions.Length == 1;
                var isMatrix = !isUAVariable || ((IUAVariable)item).ArrayDimensions.Length == 2;
                var arrayLength = isUAVariable ?
                                            ((IUAVariable)item).ArrayDimensions.Length == 0 ?
                                                string.Empty
                                                : isArray ?
                                                    ((IUAVariable)item).ArrayDimensions[0].ToString()
                                                    : ((IUAVariable)item).ArrayDimensions[0].ToString() + _arraySeparator + ((IUAVariable)item).ArrayDimensions[1].ToString()
                                            : string.Empty;
                var dynamicLink = isUAVariable ?
                                    ((DynamicLink)((IUAVariable)item).Children.GetVariable("DynamicLink")) != null ?
                                        ((DynamicLink)((IUAVariable)item).Children.GetVariable("DynamicLink")).Value.Value.ToString()
                                        : string.Empty
                                    : string.Empty;

                var value = string.Empty;

                if (isUAVariable && ((IUAVariable)item).Value.Value != null)
                {
                    var tagValue = ((IUAVariable)item).Value.Value;
                    if (isArray || isMatrix)
                    {
                        value = BackupTagArrayValue(tagValue);
                    }
                    else
                    {
                        value = BackupTagScalarValue(tagValue);
                    }
                }

                var customType = isCustomObjectTypeInstance ?
                                    GetObjectCustomeTypeBrowseName(item)
                                    : string.Empty;

                return String.Join(_csvSeparator, new List<string>() {
                type,
                browseName,
                browsePath,
                dataType,
                arrayLength,
                dynamicLink,
                value,
                customType
            });
            }
            catch (System.Exception ex)
            {
                Log.Error(MethodBase.GetCurrentMethod().Name + " Item: " + item.BrowseName, ex.Message);
                return string.Empty;
            }

        }

        private string BackupTagArrayValue(object tagValue)
        {
            var tagArray = (Array)tagValue;
            if (tagArray == null) throw new Exception("BackupAndRestoreTagValues: Unable to retrieve array values");

            var arrayRank = tagArray.Rank;
            if (arrayRank == 1) return GenerateArrayDataAsString(tagArray);
            else if (arrayRank == 2) return GenerateMatrixDataAsString(tagArray);
            else throw new NotImplementedException();
        }

        private string BackupTagScalarValue(object tagValue)
        {
            // Floating point values must be handled specifically. Note that System.Single is an alias for float.
            // See https://docs.microsoft.com/it-it/dotnet/standard/base-types/standard-numeric-format-strings#RFormatString
            // DateTime values are serialized using standard format ISO 8601.
            // See https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#the-round-trip-o-o-format-specifier
            // Other values (integer, booleans, strings) has no formatting problems.
            var tagNetType = tagValue.GetType();
            switch (Type.GetTypeCode(tagNetType))
            {
                case TypeCode.Single:
                    return String.Format(CultureInfo.InvariantCulture, "{0:G9}", tagValue);
                case TypeCode.Double:
                    return String.Format(CultureInfo.InvariantCulture, "{0:G17}", tagValue);
                case TypeCode.DateTime:
                    var date = (DateTime)tagValue;
                    if (date.Kind != DateTimeKind.Utc)
                        date = date.ToUniversalTime();
                    return date.ToString("O");
                default:
                    return tagValue.ToString();
            }
        }

        private (List<IUAObjectType>, List<IUAObject>, List<IUAVariable>) GetNodesToExport(IUANode startingNode)
        {
            var res = (new List<IUAObjectType>(), new List<IUAObject>(), new List<IUAVariable>());
            foreach (var c in startingNode.Children)
            {
                switch (c)
                {
                    case IUAObjectType _:
                        res.Item1.Add((IUAObjectType)c);
                        var nextIteration = GetNodesToExport(c);
                        res.Item1.AddRange(nextIteration.Item1);
                        res.Item2.AddRange(nextIteration.Item2);
                        res.Item3.AddRange(nextIteration.Item3);
                        break;
                    case IUAObject _: // Objects and Folders
                        res.Item2.Add((IUAObject)c);
                        var nextIterationIntoType = GetNodesToExport(c);
                        res.Item1.AddRange(nextIterationIntoType.Item1);
                        res.Item2.AddRange(nextIterationIntoType.Item2);
                        res.Item3.AddRange(nextIterationIntoType.Item3);
                        break;
                    case IUAVariable _:
                        res.Item3.Add((IUAVariable)c);
                        break;
                    default:
                        break;
                }
            }
            return res;
        }

        private string GenerateCsvHeader() => String.Join(_csvSeparator, _csvHeaderElements);

        private static string GetBrowsePath(IUANode startingNode, IUANode uANode)
        {
            var browsePath = string.Empty;
            var isStartingNode = uANode.NodeId == startingNode.NodeId;

            if (isStartingNode) return startingNode.BrowseName + browsePath;

            return GetBrowsePath(startingNode, uANode.Owner) + "/" + uANode.BrowseName;
        }

        private static IUANode GetOwnerNode(IUANode startingNode, string relativePath)
        {
            var rawPath = relativePath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var path = new string[rawPath.Length - 2];
            Array.Copy(rawPath, 1, path, 0, rawPath.Length - 2);
            var tagOwner = startingNode;
            foreach (var nodeName in path) tagOwner = tagOwner.Get(nodeName);
            return tagOwner;
        }

        private void UpdateTagValue(IUAVariable tag, string value, NodeId opcUaDataType)
        {
            var arrayLength = tag.ArrayDimensions.Length;
            var isScalar = arrayLength == 0;
            var isArray = arrayLength == 1;
            var isMatrix = arrayLength == 2;

            if (isScalar) tag.Value = ParseScalar(value, opcUaDataType);
            else if (isArray) tag.Value = ParseStringToArray(value, opcUaDataType);
            else if (isMatrix) tag.Value = ParseStringToMatrix(value, opcUaDataType);
        }

        private string GenerateArrayDataAsString(Array tagArray)
        {
            int rows = tagArray.GetLength(0);
            var res = string.Empty;
            for (int i = 0; i < rows; i++)
            {
                if (i > 0) res += _arraySeparator;
                res += BackupTagScalarValue(tagArray.GetValue(i));
            }
            return "{" + res + "}";
        }

        private string GenerateMatrixDataAsString(Array tagMatrix)
        {
            int rows = tagMatrix.GetLength(0);
            int columns = tagMatrix.GetLength(1);
            var res = string.Empty;
            for (int i = 0; i < rows; i++)
            {
                res += "{";
                for (int j = 0; j < columns; j++)
                {
                    if (j > 0) res += _arraySeparator;
                    res += BackupTagScalarValue(tagMatrix.GetValue(i, j));
                }
                res += i == rows - 1 ? "}" : "},";
            }
            return "{" + res + "}";
        }

        private UAValue ParseStringToArray(string value, NodeId opcUaDataType)
        {
            var rawValues = value.Replace("{", string.Empty).Replace("}", string.Empty).Split(_arraySeparator);
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.SByte) { return Array.ConvertAll(rawValues, SByte.Parse); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Int16) { return Array.ConvertAll(rawValues, Int16.Parse); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Int32) { return Array.ConvertAll(rawValues, Int32.Parse); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Int64) { return Array.ConvertAll(rawValues, Int64.Parse); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Byte) { return Array.ConvertAll(rawValues, Byte.Parse); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.UInt16) { return Array.ConvertAll(rawValues, UInt16.Parse); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.UInt32) { return Array.ConvertAll(rawValues, UInt32.Parse); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.UInt64) { return Array.ConvertAll(rawValues, UInt64.Parse); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Boolean) { return Array.ConvertAll(rawValues, Boolean.Parse); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Double) { return Array.ConvertAll(rawValues, Double.Parse); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Float) { return Array.ConvertAll(rawValues, float.Parse); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.String) { return rawValues; }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.DateTime) { return Array.ConvertAll(rawValues, DateTime.Parse); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Duration) { return Array.ConvertAll(rawValues, Double.Parse); }
            throw new NotImplementedException();
        }

        private UAValue ParseStringToMatrix(string value, NodeId opcUaDataType)
        {
            value = value.Trim('{', '}');
            var rows = value.Split("},{");

            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.SByte) { return GenerateTagArrayData<sbyte>(rows); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Int16) { return GenerateTagArrayData<Int16>(rows); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Int32) { return GenerateTagArrayData<Int32>(rows); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Int64) { return GenerateTagArrayData<Int64>(rows); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Byte) { return GenerateTagArrayData<Byte>(rows); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.UInt16) { return GenerateTagArrayData<UInt16>(rows); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.UInt32) { return GenerateTagArrayData<UInt32>(rows); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.UInt64) { return GenerateTagArrayData<UInt64>(rows); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Boolean) { return GenerateTagArrayData<Boolean>(rows); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Double) { return GenerateTagArrayData<Double>(rows); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Float) { return GenerateTagArrayData<float>(rows); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.String) { return GenerateTagArrayData<String>(rows); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.DateTime) { return GenerateTagArrayData<DateTime>(rows); }
            // TODO if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Duration) { return array.OfType<Duration>().ToArray(); } 
            throw new NotImplementedException();
        }

        private UAValue GenerateTagArrayData<T>(string[] rows)
        {
            var rowCount = rows.Length;
            var colCount = rows[0].Split(',').Length;
            var testArray = new T[rowCount, colCount];
            for (int i = 0; i < rowCount; i++)
            {
                var colValues = rows[i].Split(_arraySeparator);
                for (int j = 0; j < colCount; j++)
                {
                    var value = (T)Convert.ChangeType(colValues[j], typeof(T));
                    testArray[i, j] = value;
                }
            }
            return new UAValue(testArray);
        }

        private UAValue ParseScalar(string value, NodeId opcUaDataType)
        {
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.SByte) { return SByte.Parse(value); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Int16) { return Int16.Parse(value); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Int32) { return Int32.Parse(value); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Int64) { return Int64.Parse(value); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Byte) { return Byte.Parse(value); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.UInt16) { return UInt16.Parse(value); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.UInt32) { return UInt32.Parse(value); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.UInt64) { return UInt64.Parse(value); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Boolean) { return Boolean.Parse(value); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Double) { return Double.Parse(value); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Float) { return float.Parse(value); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.String) { return value; }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.DateTime) { return DateTime.Parse(value); }
            if (opcUaDataType == UAManagedCore.OpcUa.DataTypes.Duration) { return Double.Parse(value); }
            throw new NotImplementedException();
        }

        private NodeId GetOpcUaDataTypeFromStringOpcUaDataType(string opcUaDataType)
        {
            if (InformationModel.Get(UAManagedCore.OpcUa.DataTypes.SByte).BrowseName == opcUaDataType) return UAManagedCore.OpcUa.DataTypes.SByte;
            if (InformationModel.Get(UAManagedCore.OpcUa.DataTypes.Int16).BrowseName == opcUaDataType) return UAManagedCore.OpcUa.DataTypes.Int16;
            if (InformationModel.Get(UAManagedCore.OpcUa.DataTypes.Int32).BrowseName == opcUaDataType) return UAManagedCore.OpcUa.DataTypes.Int32;
            if (InformationModel.Get(UAManagedCore.OpcUa.DataTypes.Int64).BrowseName == opcUaDataType) return UAManagedCore.OpcUa.DataTypes.Int64;
            if (InformationModel.Get(UAManagedCore.OpcUa.DataTypes.Byte).BrowseName == opcUaDataType) return UAManagedCore.OpcUa.DataTypes.Byte;
            if (InformationModel.Get(UAManagedCore.OpcUa.DataTypes.UInt16).BrowseName == opcUaDataType) return UAManagedCore.OpcUa.DataTypes.UInt16;
            if (InformationModel.Get(UAManagedCore.OpcUa.DataTypes.UInt32).BrowseName == opcUaDataType) return UAManagedCore.OpcUa.DataTypes.UInt32;
            if (InformationModel.Get(UAManagedCore.OpcUa.DataTypes.UInt64).BrowseName == opcUaDataType) return UAManagedCore.OpcUa.DataTypes.UInt64;
            if (InformationModel.Get(UAManagedCore.OpcUa.DataTypes.Float).BrowseName == opcUaDataType) return UAManagedCore.OpcUa.DataTypes.Float;
            if (InformationModel.Get(UAManagedCore.OpcUa.DataTypes.Boolean).BrowseName == opcUaDataType) return UAManagedCore.OpcUa.DataTypes.Boolean;
            if (InformationModel.Get(UAManagedCore.OpcUa.DataTypes.Double).BrowseName == opcUaDataType) return UAManagedCore.OpcUa.DataTypes.Double;
            if (InformationModel.Get(UAManagedCore.OpcUa.DataTypes.String).BrowseName == opcUaDataType) return UAManagedCore.OpcUa.DataTypes.String;
            if (InformationModel.Get(UAManagedCore.OpcUa.DataTypes.DateTime).BrowseName == opcUaDataType) return UAManagedCore.OpcUa.DataTypes.DateTime;
            if (InformationModel.Get(UAManagedCore.OpcUa.DataTypes.Duration).BrowseName == opcUaDataType) return UAManagedCore.OpcUa.DataTypes.Duration;
            return UAManagedCore.OpcUa.DataTypes.BaseDataType;
        }

        private static IUANode NodeAlreadyExists(IUANode tagOwner, IUANode tag) => tagOwner.Children.FirstOrDefault(t => t.BrowseName == tag.BrowseName);

        #endregion private methods
    }
}
