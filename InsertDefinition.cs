using System;

namespace SQLiteXM
{
	public class InsertDefinition
	{
		private string tableName;
		public string TableName
		{
			get { return tableName; }
		}
		private string insertSQL;
		public string InsertSQL
		{
			get { return insertSQL; }
		}

		internal InsertDefinition (string tableName, string insertSQL)
		{
			this.tableName = tableName;
			this.insertSQL = insertSQL;
		}
	}
}

