using System;

namespace SQLiteXM
{
	public class IndexDefinition
	{
		private string indexName;
		public string IndexName
		{
			get { return indexName; }
		}
		private string indexSQL;
		public string IndexSQL
		{
			get { return indexSQL; }
		}

		internal IndexDefinition (string indexName, string indexSQL)
		{
			this.indexName = indexName;
			this.indexSQL = indexSQL;
		}
	}
}
