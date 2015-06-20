using System;

namespace SQLiteXM
{
	public class TableDefinition
	{
		private string tableSQL;
		public string TableSQL
		{
			get { return tableSQL; }
		}
		private int cloudSynch;
		public int CloudSynch
		{
			get { return cloudSynch; }
		}

		internal TableDefinition (string tableSQL, int cloudSynch)
		{
			this.tableSQL = tableSQL;
			this.cloudSynch = cloudSynch;
		}
	}
}

