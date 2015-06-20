using System;

namespace SQLiteXM
{
	public class AlterDefinition
	{
		private string columnName;
		public string ColumnName
		{
			get { return columnName; }
		}
		private string alterSQL;
		public string AlterSQL
		{
			get { return alterSQL; }
		}

		internal AlterDefinition (string columnName, string alterSQL)
		{
			this.columnName = columnName;
			this.alterSQL = alterSQL;
		}
	}
}
