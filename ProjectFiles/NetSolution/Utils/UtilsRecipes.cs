using FTOptix.HMIProject;
using FTOptix.Recipe;
using UAManagedCore;

namespace utilx.Utils
{
    internal class UtilsRecipes
    {
        private readonly IUAObject _logicObject;

        public UtilsRecipes(IUAObject logicObject)
        {
            _logicObject = logicObject;
        }

        /// <summary>
        /// Sets the defaults values to edit model variables ("reset" edit model)
        /// </summary>
        /// <param name="schemaNodeId">The schema node id.</param>
        public void SetDefaultsToEditModelVariables(NodeId schemaNodeId)
        {
            if (schemaNodeId == null)
            {
                Log.Error("SchemaNodeId parameter is empty");
                return;
            }

            var recipeSchema = InformationModel.Get<RecipeSchema>(schemaNodeId);

            if (recipeSchema == null)
            {
                Log.Error("Recipe schema not found");
                return;
            }

            var editModel = recipeSchema.GetObject("EditModel");

            if (editModel == null)
            {
                Log.Error("Edit model is null");
                return;
            }

            ResetEditModel(editModel);
        }

        /// <summary>
        /// Resets the edit model.
        /// </summary>
        /// <param name="editModel">The edit model.</param>
        private void ResetEditModel(IUANode editModel)
        {
            foreach (var item in editModel.Children)
            {
                switch (item.NodeClass)
                {
                    case NodeClass.Object:
                        ResetEditModel(item);
                        break;
                    case NodeClass.Variable:
                        ResetVariableValue(item as IUAVariable);
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Resets the variable value.
        /// </summary>
        /// <param name="iUAVariable">The i u a variable.</param>
        private static void ResetVariableValue(IUAVariable iUAVariable)
        {
            var val = iUAVariable.Value.Value;
            switch (val)
            {
                case string:
                    iUAVariable.Value = string.Empty;
                    break;
                case int:
                case short:
                case long:
                case float:
                    iUAVariable.Value = 0;
                    break;
                case bool:
                    iUAVariable.Value = false;
                    break;
                default:
                    iUAVariable.Value = 0;
                    break;
            }
        }
    }
}
