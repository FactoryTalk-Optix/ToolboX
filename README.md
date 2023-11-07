# ToolboX

## EXPERIMENTAL CONTENT - Work in progress

On Toolbox/Utils .net project you can find useful classes (as follows).
All classes expose certain methods that can be used on Runtime or Design-time NelLogics.

## Utils.cs
- MeasureMethodExecutionTime: Measures the method execution time.
- GetStringFromSbyteVariable: Gets the string from sbyte array variable.
- FromStringToSbyteArray: Generate a sbyte[] from a string

## UtilsAlarms.cs
- ClearAlarmsFolder
- GenerateDigitalAlamrsFromCommTags: Generates the digital alamrs from tags found starting from a given node

## UtilsModel.cs
- ExportModelToCsv: Exports variables and objects starting form /Model or a specific node

## UtilsProjectInformations.cs
- GetProjectInfos:
  - Project total nodes
  - Project Tags
  - Project unreferenced tags
  - List of broken dynamic links
  - List of all existing methods
  - ... more to come
 
## UtilsRecipe.cs
- SetDefaultsToEditModelVariables: Sets the defaults values to edit model variables ("reset" edit model)

## UtilsScreens.cs
- PanelLoaderHistoryManager: manages the history of a panel loader and actions like: forward, back, clear history. Minimal setup required to be used!

## UtilsStore.cs
- PopulateTableWithRandomData: Given a Store, a table name, and a number of rows to generate, it populates the table
- TruncateTableData: Truncates a table
- ... more to come

## UtilsTags.cs
- ExportToCsv
- ImportOrUpdateFromCsv
- GenerateNodesIntoModel: Generatets a set of objects and variables in model in order to have a "copy" of a set of imported tags, retrieved from a starting node
