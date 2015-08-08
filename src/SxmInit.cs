using System;
using Mono.Data.Sqlite;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace SQLiteXM
{
	public class SxmInit
	{
		private static Dictionary<string, Synchronize> synchronized = new Dictionary<string, Synchronize>();

		private SxmInit () {}

		public static bool initialize (string hrAppName, SynchOptions synchOptions = null) // Default HotRiot synchronize.
		{
			SynchSettings synchSettings = new SynchSettings (synchOptions);
			if (synchSettings.SynchErrorDel == null)
				synchSettings.SynchErrorDel = Synchronize.ErrorDel;
			synchSettings.Synch = Synchronize.Synch;

			return initialize (hrAppName, synchSettings);
		}

		public static bool initialize(Synchronize.SynchDel synch) // Custom synchronize.
		{
			SynchSettings synchSettings = new SynchSettings ();
			synchSettings.Synch = synch;

			return initialize (null, synchSettings);
		}

		public static bool initialize() // No synchronize.
		{
			return initialize (null, new SynchSettings ());
		}

		private static bool initialize(string hrAppName, SynchSettings synchSettings)
		{
			Hashtable connectionMap = new Hashtable ();
			Hashtable tableNamesMap = new Hashtable ();

			try
			{
				foreach (string key in SqlStatements.tableCreateStatements.Keys)
					if (doesTableExist (key, connectionMap, tableNamesMap) == false) 
					{
						TableDefinition tableDefinition = SqlStatements.tableCreateStatements [key] as TableDefinition;
						if (tableDefinition.TableSQL.StartsWith ("CREATE ", true, null) == true)
							applyCreateTableStatement (key, connectionMap, tableDefinition, tableNamesMap);
					}
					else
					{
						TableDefinition tableDefinition = SqlStatements.tableCreateStatements [key] as TableDefinition;
						if (tableDefinition.TableSQL.StartsWith ("DROP ", true, null) == true)
							applyDropTableStatement (key, connectionMap, tableDefinition, tableNamesMap);
						else
						{
							applyAlterTableStatements (key, connectionMap);
							applyIndexTableStatements (key, connectionMap);
						}
					} 
			}
			catch (SxmException ex)
			{
				throw ex;
			}
			catch (System.Exception ex)
			{
				throw new SxmException (ex);
			}
			finally
			{
				foreach (string databaseName in connectionMap.Keys) 
				{
					SxmConnection conn = connectionMap [databaseName] as SxmConnection;
					if (synchSettings.Synch != null) 
					{
						Synchronize synchronize = Synchronize.createSynchronize (conn, hrAppName, synchSettings);
						if (synchronize != null) 
							synchronized.Add (databaseName, synchronize);
					}
					if (conn != null)
						conn.destroyConnection ();
				}

				SqlStatements.clearStatementTables ();
			}

			return true;
		}

		public static void interruptSynchronize (string databaseName)
		{
			Synchronize synchronize = null;
			if (synchronized.TryGetValue (databaseName, out synchronize) == true)
				synchronize.interruptSynchThread ();
		}

		public static bool getSynchMonitor(string databaseName, int millisecondsTimeout)
		{
			Synchronize synchronize = null;
			if (synchronized.TryGetValue (databaseName, out synchronize) == true)
				return synchronize.getSynchMonitor (millisecondsTimeout);
			else
				return true;
		}

		public static void releaseSynchMonitor(string databaseName)
		{
			Synchronize synchronize = null;
			if (synchronized.TryGetValue (databaseName, out synchronize) == true)
				synchronize.releaseSynchMonitor ();
		}



		private static void applyCreateTableStatement (string key, Hashtable connectionMap, TableDefinition tableDefinition, Hashtable tableNamesMap)
		{
			SxmConnection sxmConnection = null;

			try
			{
				string[] parts = key.Split('.');
				sxmConnection = connectionMap [parts[0]] as SxmConnection;
				using (SxmTransaction sxmTransaction = new SxmTransaction (sxmConnection))
				{
					sxmTransaction.executeTableStatement( tableDefinition.TableSQL );
					addSynchID (parts, sxmTransaction);

					addCloudSynchDescriptor (key, tableNamesMap, sxmTransaction);
					createCloudSynchTable (key, tableNamesMap, sxmTransaction);
					createCloudSynchTriggers (key, tableNamesMap, sxmTransaction);
					sxmTransaction.commitTransaction ();
				}
				applyIndexTableStatements (key, connectionMap);
			}
			catch (SxmException ex)
			{
				throw ex;
			}
			catch (System.Exception ex) 
			{
				if (sxmConnection != null)
					sxmConnection.logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod ().ToString ());

				throw new SxmException (ex);
			}
		}

		private static void applyDropTableStatement (string key, Hashtable connectionMap, TableDefinition tableDefinition, Hashtable tableNamesMap)
		{
			SxmConnection sxmConnection = null;

			try
			{
				string[] parts = key.Split ('.');
				sxmConnection = connectionMap [parts[0]] as SxmConnection;
				using (SxmTransaction sxmTransaction = new SxmTransaction (sxmConnection))
				{
					sxmTransaction.executeTableStatement (tableDefinition.TableSQL );
					sxmTransaction.executeTableStatement (string.Format ("DROP TRIGGER IF EXISTS update{0}", parts[1]));
					sxmTransaction.executeTableStatement (string.Format ("DROP TRIGGER IF EXISTS delete{0}", parts[1]));
					sxmTransaction.commitTransaction ();
				}
			}
			catch (SxmException ex)
			{
				throw ex;
			}
			catch (System.Exception ex) 
			{
				if (sxmConnection != null)
					sxmConnection.logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod ().ToString ());

				throw new SxmException (ex);
			}
		}

		private static void applyAlterTableStatements (string key, Hashtable connectionMap)
		{
			SxmConnection sxmConnection = null;
			ArrayList alterStatementsList = SqlStatements.alterStatements [key] as ArrayList;

			if (alterStatementsList != null) 
			{
				string[] parts = key.Split ('.');

				sxmConnection = connectionMap [parts [0]] as SxmConnection;
				if (sxmConnection == null) 
				{
					sxmConnection = new SxmConnection (parts [0]);
					connectionMap.Add (parts [0], sxmConnection);
				}

				Hashtable columnNames = null;
				sxmConnection.executeQuery (String.Format ("PRAGMA table_info({0})", parts [1]), null);

				if (alterStatementsList.Count > 1) 
				{
					columnNames = new Hashtable ();
					while (sxmConnection.nextRow () == true) 
						columnNames.Add ((string)sxmConnection.getValue ("name"), new Object());
				}

				foreach (AlterDefinition alterDefinition in alterStatementsList) 
				{
					bool columnFound = false;

					if (columnNames != null)
					{
						if (columnNames [alterDefinition.ColumnName] != null)
							columnFound = true;
					}
					else
					{
						while (sxmConnection.nextRow () == true) 
						{
							string columnName = (string)sxmConnection.getValue ("name");
							if (columnName.Equals (alterDefinition.ColumnName) == true) 
							{
								columnFound = true;
								break;
							}
						}
					}

					if (columnFound == false) 
					{
						try
						{
							using (SxmTransaction sxmTransaction = new SxmTransaction (sxmConnection))
							{
								sxmTransaction.executeAlterTable (alterDefinition.AlterSQL);
								sxmTransaction.commitTransaction ();
							}
						}
						catch (SxmException ex)
						{
							throw ex;
						}
						catch (System.Exception ex) 
						{
							sxmConnection.logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod ().ToString ());
							throw new SxmException (ex);
						}
					}
				}
			}
		}

		private static void applyIndexTableStatements (string key, Hashtable connectionMap)
		{
			ArrayList indexStatementsList = SqlStatements.indexStatements [key] as ArrayList;

			if (indexStatementsList != null) 
			{
				string[] parts = key.Split ('.');
				SxmConnection sxmConnection = connectionMap [parts [0]] as SxmConnection;
				if (sxmConnection == null) 
				{
					sxmConnection = new SxmConnection (parts [0]);
					connectionMap.Add (parts [0], sxmConnection);
				}


				Hashtable indexNames = null;
				sxmConnection.executeQuery (String.Format ("PRAGMA index_list({0})", parts [1]), null);

				if (indexStatementsList.Count > 1) 
				{
					indexNames = new Hashtable ();
					while (sxmConnection.nextRow () == true) 
						indexNames.Add ((string)sxmConnection.getValue ("name"), new Object());
				}

				foreach (IndexDefinition indexDefinition in indexStatementsList) 
				{
					bool indexFound = false;
					bool runit = false;

					if (indexNames != null)
					{
						if (indexNames [indexDefinition.IndexName] != null)
							indexFound = true;
					}
					else
					{
						while (sxmConnection.nextRow () == true) 
						{
							string indexName = (string)sxmConnection.getValue ("name");
							if (indexName.Equals (indexDefinition.IndexName) == true) 
							{
								indexFound = true;
								break;
							}
						}
					}

					if (indexFound == false && indexDefinition.IndexSQL.StartsWith ("CREATE ", true, null) == true) 
					{
						if (dropExists (indexStatementsList, indexDefinition.IndexName) == false)
							runit = true;
					}
					else
						if (indexFound == true && indexDefinition.IndexSQL.StartsWith ("DROP ", true, null) == true)
							runit = true;

					if (runit == true)
					{
						try
						{
							using (SxmTransaction sxmTransaction = new SxmTransaction (sxmConnection))
							{
								sxmTransaction.executeIndex (indexDefinition.IndexSQL);
								sxmTransaction.commitTransaction ();
							}
						}
						catch (SxmException ex)
						{
							throw ex;
						}
						catch (System.Exception ex) 
						{
							sxmConnection.logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod ().ToString ());
							throw new SxmException (ex);
						}
					}
				}
			}
		}

		private static bool dropExists (ArrayList indexStatementsList, string indexName)
		{
			foreach (IndexDefinition indexDefinition in indexStatementsList) 
			{
				if (indexDefinition.IndexName.Equals (indexName) == true)
					if (indexDefinition.IndexSQL.StartsWith ("DROP ", true, null) == true)
						return true;
			}

			return false;
		}

		private static void addSynchID (string[] parts, SxmTransaction sxmTransaction)
		{
			string alterSQL = String.Format ("ALTER TABLE {0} ADD COLUMN systemSynchID TEXT NOT NULL DEFAULT ''", parts [1]);
			sxmTransaction.executeAlterTable (alterSQL);
		}

		private static void addCloudSynchDescriptor (string key, Hashtable tableNamesMap, SxmTransaction sxmTransaction)
		{
			string[] parts = key.Split('.');
			string databaseName = parts[0];
			string databaseTable = "_systemCloudSynchDescriptor";

			if (isTableInMap (databaseName, databaseTable, tableNamesMap) == false) 
			{
				string tableSQL = String.Format("CREATE TABLE {0} (id INTEGER PRIMARY KEY AUTOINCREMENT, dbName TEXT, tableName TEXT, cloudSynchFlag INTEGER)", databaseTable);
				sxmTransaction.executeTableStatement ( tableSQL );
				ArrayList dbTableNames = tableNamesMap[databaseName] as ArrayList;
				dbTableNames.Add (databaseTable);
			}

			TableDefinition tableDefinition = SqlStatements.tableCreateStatements [key] as TableDefinition;
			ArrayList parameterValues = new ArrayList ();
			parameterValues.Add (databaseName);
			parameterValues.Add (parts[1]);
			parameterValues.Add (tableDefinition.CloudSynch);
			sxmTransaction.executeSystemUpdateDirect ("INSERT INTO _systemCloudSynchDescriptor (dbName, tableName, cloudSynchFlag) VALUES(?, ?, ?)", parameterValues);
		}

		private static void createCloudSynchTable (string key, Hashtable tableNamesMap, SxmTransaction sxmTransaction)
		{
			string[] parts = key.Split('.');
			string databaseName = parts[0];
			string databaseTable = "_systemCloudSynch";

			if (isTableInMap (databaseName, databaseTable, tableNamesMap) == false) 
			{
				string tableSQL = String.Format("CREATE TABLE {0} (id INTEGER PRIMARY KEY AUTOINCREMENT, dbName TEXT, tableName TEXT, action TEXT, systemSynchID TEXT)", databaseTable);
				sxmTransaction.executeTableStatement ( tableSQL );
				ArrayList dbTableNames = tableNamesMap[databaseName] as ArrayList;
				dbTableNames.Add (databaseTable);
			}
		}

		private static void createCloudSynchTriggers (string key, Hashtable tableNamesMap, SxmTransaction sxmTransaction)
		{
			string[] parts = key.Split('.');
			string databaseName = parts[0];
			string databaseTable = parts[1];

			TableDefinition tableDefinition = SqlStatements.tableCreateStatements [key] as TableDefinition;

			if (tableDefinition.CloudSynch != Defines.NO_CLOUD_SYNCH)
			{
				string tableSQL = String.Format ("CREATE TRIGGER IF NOT EXISTS update{0} UPDATE ON {0} BEGIN INSERT INTO _systemCloudSynch (dbName, tableName, action, systemSynchID) VALUES ('{1}', '{0}', 'update', new.systemSynchID); END;", databaseTable, databaseName);
				sxmTransaction.executeCreateTrigger ( tableSQL );
				if (tableDefinition.CloudSynch == Defines.CLOUD_SYNCH) 
				{
					tableSQL = String.Format ("CREATE TRIGGER IF NOT EXISTS delete{0} DELETE ON {0} BEGIN INSERT INTO _systemCloudSynch (dbName, tableName, action, systemSynchID) VALUES ('{1}', '{0}', 'delete', old.systemSynchID); END;", databaseTable, databaseName);
					sxmTransaction.executeCreateTrigger (tableSQL);
				}
			}
		}

		private static bool doesTableExist (string key, Hashtable connectionList, Hashtable tableNamesMap)
		{
			string[] parts = key.Split('.');
			if (parts.Length != 2) 
			{
				throw new SxmException (new ErrorMessage("invalidTableName", key));
			}
			else
			{
				string databaseName = parts[0];
				string databaseTable = parts[1];

				SxmConnection sxmConnection = connectionList [databaseName] as SxmConnection;
				if (sxmConnection == null) 
				{
					sxmConnection = new SxmConnection (databaseName);
					connectionList.Add (databaseName, sxmConnection);

					sxmConnection.executeQuery ("SELECT name FROM sqlite_master WHERE type='table'", null);

					ArrayList tableNames = new ArrayList ();
					if (sxmConnection.hasRows() == true)
					{
						string[] fieldNames = sxmConnection.getFieldNames ();
						while (sxmConnection.nextRow () == true) 
						{
							foreach (string fieldName in fieldNames)
								tableNames.Add (sxmConnection.getValue (fieldName));
						}
					}

					tableNamesMap.Add (databaseName, tableNames);

				}

				if (isTableInMap (databaseName, databaseTable, tableNamesMap) == true)
					return true;
			}

			return false;
		}

		private static bool isTableInMap (string databaseName, string tableName, Hashtable tableNamesMap)
		{
			ArrayList dbTableNames = tableNamesMap[databaseName] as ArrayList;
			if (dbTableNames != null) 
			{
				foreach (string dbTableName in dbTableNames) 
				{
					if (dbTableName.Equals (tableName) == true)
						return true;
				}
			}

			return false;
		}
	}
}
	