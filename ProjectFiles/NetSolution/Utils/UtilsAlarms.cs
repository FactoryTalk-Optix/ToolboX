using FTOptix.Alarm;
using FTOptix.CommunicationDriver;
using FTOptix.Core;
using FTOptix.CoreBase;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UAManagedCore;

namespace utilx.Utils
{
    internal class UtilsAlarms
    {
        private readonly IUAObject _logicObject;
        private readonly Folder _alarmsFolder = Project.Current.Get<Folder>("Alarms");

        public UtilsAlarms(IUAObject logicObject)
        {
            _logicObject = logicObject;
        }

        /// <summary>
        /// Clears the alarms folder from AlarmController instances
        /// </summary>
        public void ClearAlarmsFolder()
        {
            var alarms = _alarmsFolder.FindNodesByType<AlarmController>().ToList();
            alarms.ForEach(alarm => alarm.Delete());
        }

        /// <summary>
        /// Generates the digital alamrs from tags found starting from a given node
        /// </summary>
        /// <param name="startingNodeId">The starting node id.</param>
        public void GenerateDigitalAlamrsFromCommTags(NodeId startingNodeId)
        {
            var startingNode = InformationModel.Get(startingNodeId);

            if (startingNode == null)
            {
                Log.Error(MethodBase.GetCurrentMethod().Name, "cannot find starting node");
                return;
            }

            if (startingNode is Tag)
            {
                GenerateDigitalAlarms((Tag)startingNode);
                return;
            }

            var tags = startingNode.FindNodesByType<Tag>().ToList();
            tags.ForEach(tag => GenerateDigitalAlarms(tag));

        }

        #region private methods

        /// <summary>
        /// Generates digital alarms from a specific UAVariable
        /// </summary>
        /// <param name="variable">The variable.</param>
        private void GenerateDigitalAlarms(UAVariable variable)
        {
            if (variable == null)
            {
                Log.Error(MethodBase.GetCurrentMethod().Name, "Variable is null");
                return;
            }

            var tagDataType = variable.DataType;
            var numberOfAlarmsToGenerate = 1;
            var variableIsArray = variable.ArrayDimensions.Length == 1;
            var isBoolVariable = variable.Value.Value is bool;

            if (UAManagedCore.OpcUa.DataTypes.Int16 == tagDataType) numberOfAlarmsToGenerate = sizeof(short) * 8;
            if (UAManagedCore.OpcUa.DataTypes.Int32 == tagDataType) numberOfAlarmsToGenerate = sizeof(int) * 8;
            if (UAManagedCore.OpcUa.DataTypes.UInt16 == tagDataType) numberOfAlarmsToGenerate = sizeof(ushort) * 8;
            if (UAManagedCore.OpcUa.DataTypes.UInt32 == tagDataType) numberOfAlarmsToGenerate = sizeof(uint) * 8;

            var alarmsFolderTemp = GetAlarmFolder(variable, variableIsArray, isBoolVariable);
            DigitalAlarm alm;

            if (variableIsArray)
            {
                var arrDimention = variable.ArrayDimensions[0];
                var counter = 0;
                for (uint i = 0; i < arrDimention; i++)
                {
                    if (isBoolVariable)
                    {
                        alm = GenerateDigitalAlarm(variable, i);
                        if (!NodeAlreadyExists(alarmsFolderTemp, alm)) alarmsFolderTemp.Add(alm);
                    }
                    else
                    {
                        var subFolder = InformationModel.Make<Folder>(counter.ToString());
                        alarmsFolderTemp.Add(subFolder);

                        for (uint j = 0; j < numberOfAlarmsToGenerate; j++)
                        {
                            alm = GenerateDigitalAlarm(variable, i, j);
                            if (!NodeAlreadyExists(subFolder, alm))
                            {
                                AddArrayIndexToDynamicLink(alm, j);
                                subFolder.Add(alm);
                            }
                        }
                        counter++;
                    }
                }
            }
            else
            {
                for (uint i = 0; i < numberOfAlarmsToGenerate; i++)
                {
                    alm = isBoolVariable ? GenerateDigitalAlarm(variable) : GenerateDigitalAlarm(variable, i);
                    if (!NodeAlreadyExists(alarmsFolderTemp, alm))
                    {
                        if (!isBoolVariable)
                        {
                            AddArrayIndexToDynamicLink(alm, i);
                        }
                        alarmsFolderTemp.Add(alm);
                    }
                }
            }
        }


        /// <summary>
        /// Generates the digital alarm instance linked to a specific UAVariable
        /// </summary>
        /// <param name="variable">The variable.</param>
        /// <param name="rowIndex">The row index.</param>
        /// <param name="columnIndex">The column index.</param>
        /// <returns>A DigitalAlarm.</returns>
        private static DigitalAlarm GenerateDigitalAlarm(UAVariable variable, uint? rowIndex = null, uint? columnIndex = null)
        {
            var isArrayVar = rowIndex != null;
            var isMatrixVar = isArrayVar && columnIndex != null;
            var isScalarVar = rowIndex == null && columnIndex == null;

            var alarmName = $"DAlm_{variable.Owner.BrowseName}_{variable.BrowseName}";
            alarmName = isMatrixVar ? $"{alarmName}_{rowIndex}_{columnIndex}" : isArrayVar? $"{alarmName}_{rowIndex}" : alarmName;
            var digitalAlarm = InformationModel.Make<DigitalAlarm>(alarmName);

            if (isArrayVar)
            {
                digitalAlarm.InputValueVariable.SetDynamicLink(variable, (uint)rowIndex, DynamicLinkMode.ReadWrite);
            }
            else if (isScalarVar)
            {
                digitalAlarm.InputValueVariable.SetDynamicLink(variable, DynamicLinkMode.ReadWrite);
            }
            return digitalAlarm;
        }

        /// <summary>
        /// Adds the bit array index to dynamic link.
        /// </summary>
        /// <param name="alm">The alm.</param>
        /// <param name="index">The index.</param>
        /// <returns>An IUAVariable.</returns>
        private static IUAVariable AddArrayIndexToDynamicLink(DigitalAlarm alm, uint index)
        {
            var almDl = alm.InputValueVariable.GetVariable("DynamicLink");
            almDl.Value = almDl.Value + "." + index;
            return almDl;
        }

        /// <summary>
        /// Check if a node already exists as child of another one
        /// </summary>
        /// <param name="parent">The parent.</param>
        /// <param name="child">The child.</param>
        /// <returns>A bool.</returns>
        private static bool NodeAlreadyExists(IUANode parent, IUANode child)
        {
            var res = parent.Children.Any(c => c.BrowseName == child.BrowseName);
            if (res) Log.Warning(child.BrowseName + " already exists into " + parent.BrowseName);
            return res;
        }

        /// <summary>
        /// Get the alarm folder for a specific alarm
        /// </summary>
        /// <param name="variable">The variable.</param>
        /// <param name="isArrayVariable">If true, is array variable.</param>
        /// <param name="isBoolVariable">If true, is bool variable.</param>
        /// <returns>A Folder.</returns>
        private Folder GetAlarmFolder(UAVariable variable, bool isArrayVariable, bool isBoolVariable)
        {
            if (!isArrayVariable && isBoolVariable) return _alarmsFolder;
            var variableAlmsFolder = InformationModel.Make<Folder>($"{variable.Owner.BrowseName}_{variable.BrowseName}_alarms");
            if (NodeAlreadyExists(_alarmsFolder, variableAlmsFolder)) variableAlmsFolder = _alarmsFolder.Children.Get<Folder>(variableAlmsFolder.BrowseName);
            _alarmsFolder.Add(variableAlmsFolder);
            return variableAlmsFolder;
        }


        #endregion private methods
    }
}
