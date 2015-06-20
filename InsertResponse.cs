using System;

namespace SQLiteXM
{
	public class InsertResponse
	{
		private long recordID;
		public long RecordID
		{
			get { return recordID; }
		}
		private string synchID;
		public string SynchID
		{
			get { return synchID; }
		}

		internal InsertResponse (long recordID, string synchID)
		{
			this.recordID = recordID;
			this.synchID = synchID;
		}
	}
}

