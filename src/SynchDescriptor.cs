﻿using System;
using System.Collections;

namespace SQLiteXM
{
	public class SynchDescriptor
	{
		public string action;
		public string dbName;
		public string tableName;
		public SynchResponse synchResponse;
		public Hashtable recordDataToSynch;

		public SynchDescriptor (string action, string dbName, string tableName, Hashtable recordDataToSynch)
		{
			this.action = action;
			this.dbName = dbName;
			this.tableName = tableName;
			this.recordDataToSynch = recordDataToSynch;
		}

		public SynchDescriptor (string action, string dbName, string tableName)
		{
			this.action = action;
			this.dbName = dbName;
			this.tableName = tableName;
			this.recordDataToSynch = new Hashtable ();
		}

		public void addRecordDataElement (string key, object value)
		{
			recordDataToSynch.Add (key, value);
		}
	}
}

