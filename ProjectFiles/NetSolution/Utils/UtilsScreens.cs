using FTOptix.HMIProject;
using FTOptix.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UAManagedCore;

namespace utilx.Utils
{
    /// <summary>
    /// The utils useful for Screen instances management
    /// </summary>
    internal class UtilsScreens
    {
        private readonly IUAObject _logicObject;

        public UtilsScreens(IUAObject logicObject)
        {
            _logicObject = logicObject;
        }

        public static PanelLoaderHistoryManager CreatePanelLoaderHistoryManager(NodeId panelLoaderId)
        {
            PanelLoaderHistoryManager panelLoaderHistoryManager = new PanelLoaderHistoryManager(panelLoaderId);
            return panelLoaderHistoryManager;
        }

    }

    /// <summary>
    /// The panel loader history manager.
    /// </summary>
    internal class PanelLoaderHistoryManager
    {
        private readonly PanelLoader _panelLoader;
        private readonly Stack<string> _screensStack = new();
        private int _currentStackIndex = 0;
        private bool _historyBack;
        private bool _historyForward;

        public PanelLoaderHistoryManager(NodeId panelLoaderId)
        {
            _panelLoader = InformationModel.Get<PanelLoader>(panelLoaderId);
            if (panelLoaderId == null || _panelLoader == null)
            {
                Log.Error(MethodBase.GetCurrentMethod().Name, "Cannot obtain PanelLoader");
            }

            _panelLoader.CurrentPanelVariable.VariableChange += HistoryAdd;
            InitializeScreensStack();
        }

        private void InitializeScreensStack()
        {
            _screensStack.Push(GetCurrentScreenBrowsePath());
        }

        /// <summary>
        /// Add new item to screen navigation history
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
        private void HistoryAdd(object sender, VariableChangeEventArgs e)
        {
            if (_historyBack || _historyForward)
            {
                _historyBack = false;
                _historyForward = false;
                return;
            }

            if (_currentStackIndex != 0)
            {
                for (int i = _currentStackIndex; i > 0; i--)
                {
                    _screensStack.Pop();
                }
                _currentStackIndex = 0;
            }

            var getCurrentScreenBrowsePath = GetCurrentScreenBrowsePath();
            if (_screensStack.Count != 0 && _screensStack.ElementAt(0) == getCurrentScreenBrowsePath)
            {
                return;
            }
            _screensStack.Push(getCurrentScreenBrowsePath);
        }

        public void HistoryBack()
        {
            _currentStackIndex = Math.Min(_screensStack.Count - 1, _currentStackIndex + 1);
            UpdatePanelLoaderCurrentScreen();
            _historyBack = true;
        }

        public void HistoryForward()
        {
            _currentStackIndex = Math.Max(0, _currentStackIndex - 1);
            UpdatePanelLoaderCurrentScreen();
            _historyForward = true;
        }

        public void ClearHistory()
        {
            _screensStack.Clear();
            _currentStackIndex = 0;
            InitializeScreensStack();
        }

        private void UpdatePanelLoaderCurrentScreen()
        {
            var screen = GetScreenFromBrowsePath(_screensStack.ElementAt(_currentStackIndex));
            _panelLoader.PanelVariable.Value = screen.NodeId;
        }

        /// <summary>
        /// Retrieve the BrowsePath of the PanelLoader active Panel, relative to the default Screen folder
        /// </summary>
        /// <returns></returns>
        private string GetCurrentScreenBrowsePath()
        {
            var currentScreenBrowsePathSegments = Log.Node(InformationModel.Get(_panelLoader.PanelVariable.Value)).Split("/").ToList();
            currentScreenBrowsePathSegments.RemoveRange(0, 3);
            return string.Join('/', currentScreenBrowsePathSegments);
        }

        private static IUANode GetScreenFromBrowsePath(string screenBrowsePath) => Project.Current.Get(screenBrowsePath);
    }
}
