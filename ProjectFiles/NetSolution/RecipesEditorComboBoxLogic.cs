#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.Retentivity;
using FTOptix.NativeUI;
using FTOptix.NetLogic;
using FTOptix.UI;
using FTOptix.Recipe;
using FTOptix.Core;
using FTOptix.CoreBase;
using FTOptix.SQLiteStore;
using FTOptix.Store;
using FTOptix.EventLogger;
using FTOptix.CODESYS;
using FTOptix.DataLogger;
#endregion

public class RecipesEditorComboBoxLogic : BaseNetLogic
{
	public override void Start()
	{
		comboBox = (ComboBox)Owner;
		comboBox.SelectedValueVariable.VariableChange += SelectedValueVariable_VariableChange;
		LoadSelectedRecipeData();
	}

	private void SelectedValueVariable_VariableChange(object sender, VariableChangeEventArgs e)
	{
		LoadSelectedRecipeData();
	}

	private void LoadSelectedRecipeData()
	{
		LocalizedText selectedText = (LocalizedText)comboBox.SelectedValue;
		if (selectedText == null || string.IsNullOrEmpty(selectedText.Text))
			return;

		var recipeSchemaEditor = Owner.Owner;
		var recipeSchemaVariable = recipeSchemaEditor.GetVariable("RecipeSchema");
		if (recipeSchemaVariable == null)
			return;

		var recipeSchemaNodeId = (NodeId)recipeSchemaVariable.Value.Value;

		var recipeSchemaObject = (RecipeSchema)InformationModel.Get(recipeSchemaNodeId);
		if (recipeSchemaObject == null)
			return;

		var editModelNode = recipeSchemaObject.GetObject("EditModel");
		if (editModelNode == null)
			return;

		recipeSchemaObject.CopyFromStoreRecipe(comboBox.Text, editModelNode.NodeId, CopyErrorPolicy.BestEffortCopy);
	}

	public override void Stop()
	{
		comboBox.SelectedValueVariable.VariableChange -= SelectedValueVariable_VariableChange;
	}

	private ComboBox comboBox;
}
