using FTOptix.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UAManagedCore;

namespace utilx.Utils
{
    class UtilsStore
    {
        private readonly IUAObject _logicObject;
        private readonly Store _store;

        public UtilsStore(IUAObject logicObject, Store store)
        {
            _logicObject = logicObject;
            _store = store;
        }

        /// <summary>
        /// Given a Store, a table name, and a number of rows to generate, it populates the table
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="rowsToGenerate"></param>
        public void PopulateTableWithRandomData(string tableName, int rowsToGenerate)
        {
            var tableColumnsNames = GetTableColumnsNames(tableName);
            var tableColumnsTypes = GetTableColumnsTypes(tableName);

            for (int i = 0; i < rowsToGenerate; i++)
            {
                object[,] values = GenerateTableRowData(tableColumnsTypes);
                _store.Insert(tableName, tableColumnsNames.ToArray(), values);
            }
        }

        /// <summary>
        /// Truncates a table
        /// </summary>
        /// <param name="tableName"></param>
        public void TruncateTableData(string tableName)
        {
            _store.Query($"DELETE FROM {tableName}", out string[] header, out object[,] res);
        }

        private static object[,] GenerateTableRowData(List<Type> tableColumnsTypes)
        {
            Random random = new Random();
            object[,] values = new object[1, tableColumnsTypes.Count];
            var rowData = tableColumnsTypes.Select(rowType => GetRandomValue(rowType, random)).ToList();

            for (int i = 0; i < tableColumnsTypes.Count; i++)
            {
                values[0, i] = rowData[i];
            }

            return values;
        }

        private static object GetRandomValue(Type dataType, Random random)
        {
            if (dataType == typeof(int)) return random.Next(1, 100);
            if (dataType == typeof(float)) return random.NextDouble() * 100.0;
            if (dataType == typeof(string)) return Guid.NewGuid().ToString();
            if (dataType == typeof(bool)) return random.Next(2);
            return null;
        }

        private List<string> GetTableColumnsNames(string tableName)
        {
            var table = _store.Tables.FirstOrDefault(t => t.BrowseName == tableName);
            var columnNames = new List<string>();

            foreach (var column in table.Columns)
            {
                columnNames.Add(column.BrowseName);
            }

            return columnNames;
        }

        private List<Type> GetTableColumnsTypes(string tableName)
        {
            var table = _store.Tables.FirstOrDefault(t => t.BrowseName == tableName);
            var columnNames = new List<Type>();

            foreach (var column in table.Columns)
            {
                columnNames.Add(column.Value.Value.GetType());
            }

            return columnNames;
        }
    }
}
