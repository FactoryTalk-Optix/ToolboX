using FTOptix.HMIProject;
using FTOptix.NetLogic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UAManagedCore;

namespace utilx.Utils
{
    internal class Utils
    {
        private readonly IUAObject _logicObject;

        public Utils(IUAObject logicObject)
        {
            _logicObject = logicObject;
        }

        /// <summary>
        /// Measures the method execution time.
        /// </summary>
        /// <param name="method">The method.</param>
        public static void MeasureMethodExecutionTime<T>(Func<T> method)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            stopwatch.Start();

            method.Invoke();

            stopwatch.Stop();

            var elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

            Log.Info(method.Method.Name + " elapsed milliseconds: " + elapsedMilliseconds);
        }


        /// <summary>
        /// Gets the string from sbyte array variable.
        /// </summary>
        /// <param name="varNodeId">The var node id.</param>
        /// <param name="res">The res.</param>
        public static void GetStringFromSbyteVariable(NodeId varNodeId, out string res)
        {
            var v = InformationModel.GetVariable(varNodeId);
            var sbyteVar = v.Value.Value;

            if (sbyteVar == null
                || sbyteVar is not Array
                || (sbyteVar as Array).GetValue(0) is not sbyte)
            {
                throw new Exception("FromSbyteToString: cannot parse " + v.BrowseName);
            }

            var byteArray = (byte[])(sbyteVar as Array);
            res = Encoding.Unicode.GetString(byteArray);
        }
    }
}
      
