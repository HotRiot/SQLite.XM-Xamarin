using System;
using System.IO;
using Mono.Data.Sqlite;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SQLiteXM
{
	public class SxmTransaction : IDisposable
	{
		private SxmConnection connection;
		bool disposed = false;

		public SxmTransaction (SxmConnection connection)
		{
			try
			{
				if (connection.lockConnection () == false) 
				{
					throw new SxmException (new ErrorMessage("lockDB", connection.DatabaseName));
				}
				this.connection = connection;
			}
			catch (SxmException ex)
			{
				if (connection != null) 
					connection.logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod ().ToString ());
				throw ex;
			}
			catch(System.Exception ex)
			{
				if (connection != null) 
					connection.logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod ().ToString ());
				throw new SxmException (ex);
			}
		}

		public SxmTransaction (string databaseName = null)
		{
			bool transient = true;

			try
			{
				connection = new SxmConnection(databaseName, transient);
				if (connection.lockConnection () == false) 
				{
					throw new SxmException (new ErrorMessage("lockDB", databaseName));
				}
			}
			catch (SxmException ex)
			{
				if (connection != null) 
				{
					connection.logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod ().ToString ());
					connection.releaseConnection ();
				}
				throw ex;
			}
			catch(System.Exception ex)
			{
				if (connection != null) 
				{
					connection.logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod ().ToString ());
					connection.releaseConnection ();
				}
				throw new SxmException (ex);
			}
		}

		public void Dispose()
		{ 
			Dispose(true); // Called from user code.
			GC.SuppressFinalize(this);           
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposed == true)
				return;

			if (disposing == true) {/* Called from user code. Release managed and unmanaged resources. */} 

			finalizeTransaction ();
			disposed = true;
		}

		~SxmTransaction()
		{
			Dispose (false); // Called from runtime.
		}

		public InsertResponse executeInsert (string command, ArrayList ParameterValues)
		{
			long recordID = -1;
			string synchID = null;

			InsertDefinition insertDefinition = SqlStatements.insertStatements [command] as InsertDefinition;
			if (insertDefinition == null)
				throw new SxmException ( new ErrorMessage("unknownSQLStatement", command));
			executeNonQueryTrans (insertDefinition.InsertSQL, ParameterValues);

			try
			{
				if (insertDefinition.TableName.Length != 0) 
				{
					executeQueryDirect ("select last_insert_rowid() as rowID", null);
					Hashtable nextRow = connection.getNextRow ();
					recordID = (long)nextRow ["rowID"];
					synchID = getSynchID (insertDefinition.TableName, recordID);

					if (synchID == null || synchID.Length == 0)
						synchID = Guid.NewGuid ().ToString ();						
							
					ArrayList synchIDPV = new ArrayList ();
					synchIDPV.Add (synchID);
					synchIDPV.Add (recordID);
					executeNonQuery (String.Format ("UPDATE {0} SET systemSynchID = ? WHERE id = ?", insertDefinition.TableName), synchIDPV);
					synchIDPV.RemoveAt (1);

					executeNonQuery (String.Format ("UPDATE _systemCloudSynch SET action='insert' WHERE systemSynchID = ? "), synchIDPV);
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

			return new InsertResponse (recordID, synchID);
		}

		private string getSynchID (string tableName, long recordID)
		{
			Hashtable row = null;
			string systemSynchID = null;

			try
			{
				ArrayList parameterList = new ArrayList ();
				parameterList.Add (recordID);

				executeQueryDirect (String.Format ("SELECT systemSynchID FROM {0} WHERE id = ? LIMIT 1", tableName), parameterList);
				row = connection.getNextRow ();

				if (row != null && row.Count > 0) 
					if (row.ContainsKey ("systemSynchID") == true)
						systemSynchID = (string)row ["systemSynchID"];

			}
			#pragma warning disable 0168
			catch (Exception doNothing) { /* If an error occurs reading the record, then do nothing. Assume synch ID does not exist. */ }
			#pragma warning restore 0168

			return systemSynchID;
		}

		public void executeQueryDirect (string sqlStatement, ArrayList ParameterValues)
		{
			connection.executeQuery (sqlStatement, ParameterValues);
		}

		public void executeQuery (string command, ArrayList ParameterValues)
		{
			connection.executeQuery (SqlStatements.selectStatements [command], ParameterValues);
		}

		public void executeUpdateDirect (string sqlStatement, ArrayList ParameterValues)
		{
			executeNonQuery (sqlStatement, ParameterValues);
		}

		public void executeUpdate (string command, ArrayList ParameterValues)
		{
			executeNonQuery (SqlStatements.updateStatements [command], ParameterValues);
		}

		public void executeDeleteDirect (string sqlStatement, ArrayList ParameterValues)
		{
			executeNonQuery (sqlStatement, ParameterValues);
		}

		public void executeDelete (string command, ArrayList ParameterValues)
		{
			executeNonQuery (SqlStatements.deleteStatements [command], ParameterValues);
		}

		public void executeSystemUpdateDirect (string sqlStatement, ArrayList ParameterValues)
		{
			executeNonQueryTrans (sqlStatement, ParameterValues);
		}

		public void executeTableStatement (string sqlStatement)
		{
			executeNonQueryTrans (sqlStatement);
		}

		public void executeAlterTable (string sqlStatement)
		{
			executeNonQueryTrans (sqlStatement);
		}

		public void executeIndex (string sqlStatement)
		{
			executeNonQueryTrans (sqlStatement);
		}

		public void executeCreateTrigger (string sqlStatement)
		{
			executeNonQueryTrans (sqlStatement);
		}

		public void executeNonQuery (string sqlStatement, ArrayList ParameterValues = null)
		{
			executeNonQueryTrans (sqlStatement, ParameterValues);
			SxmInit.interruptSynchronize (connection.DatabaseName);
		}

		public void executeNonQueryTrans (string sqlStatement, ArrayList ParameterValues = null)
		{
			connection.beginTransaction ();
			connection.executeNonQuery (sqlStatement, ParameterValues);
		}

		public void attachDatabase ()
		{
			ArrayList databaseNames = DatabaseDescriptor.getDatabaseNames ();

			foreach (string databaseName in databaseNames)
				attachDatabase (databaseName);
		}

		// Silent when attempting to attach to the current connection.
		public void attachDatabase (string databaseName)
		{
			if (connection.DatabaseName.Equals (databaseName) == false) 
			{
				DatabaseDescriptor databaseDescriptor = DatabaseDescriptor.getDescriptor (databaseName);
				if (databaseDescriptor == null) 
					throw new SxmException (new ErrorMessage("noDBDescriptorExists", databaseName));

				try
				{
					string databaseFolderPath = Environment.GetFolderPath (databaseDescriptor.DatabaseFolder);
					string dbFullyQualifiedPath = Path.Combine (databaseFolderPath, databaseName);

					if (File.Exists (dbFullyQualifiedPath) == true)
						connection.executeNonQuery (String.Format ("ATTACH DATABASE '{0}' as {1}", dbFullyQualifiedPath, databaseName), null);
					else
						throw new SxmException (new ErrorMessage("noDatabaseExists", databaseName));
				}
				catch (SxmException ex) 
				{
					throw ex;
				}
				catch (System.Exception ex) 
				{
					throw new SxmException (ex);
				}
			}
		}

		// Detach all attached databases. Detaching all databases is normally associated with cleanup, no-throw.
		public void detachDatabase ()
		{
			try
			{
				connection.executeQuery ("PRAGMA database_list", null);

				while (nextRow () == true) 
				{
					try
					{
						string dbName = (string)getValue ("name");
						if (dbName.ToLower().Equals("main") == false && dbName.ToLower().Equals("temp") == false)
							detachDatabase (dbName);
					}
					#pragma warning disable 0168
					catch (System.Exception notUsed) // Keep trying to detach all databases.
					#pragma warning restore 0168
					{
					}
				}
			}
			#pragma warning disable 0168
			catch (System.Exception notUsed) 
			#pragma warning restore 0168
			{
			}
		}

		// Silent when attempting to detach to the current connection.
		public void detachDatabase (string databaseName)
		{
			if (connection.DatabaseName.Equals (databaseName) == false) 
			{
				DatabaseDescriptor databaseDescriptor = DatabaseDescriptor.getDescriptor (databaseName);
				if (databaseDescriptor == null)
					throw new SxmException (new ErrorMessage("noDBDescriptorExists", databaseName));

				try
				{
					string databaseFolderPath = Environment.GetFolderPath (databaseDescriptor.DatabaseFolder);
					string dbFullyQualifiedPath = Path.Combine (databaseFolderPath, databaseName);
					if (File.Exists (dbFullyQualifiedPath) == true)
						connection.executeNonQuery (String.Format ("DETACH DATABASE '{0}'", databaseName), null);
					else
						throw new SxmException (new ErrorMessage("noDatabaseExists", databaseName));
				}
				catch (SxmException ex) 
				{
					throw ex;
				}
				catch (System.Exception ex) 
				{
					throw new SxmException (ex);
				}
			}
		}

		// Returns error code for SqliteException, otherwise throw the exception.
		public SQLiteErrorCode commitTransaction ()
		{
			return connection.finishTransaction (SQLiteXM.Defines.commitTransaction);
		}

		public void rollbackTransaction ()
		{
			connection.finishTransaction (SQLiteXM.Defines.rollbackTransaction);
		}

		// No-throw guarantee.
		protected void finalizeTransaction ()
		{
			try 
			{
				connection.releaseConnection ();
			} 
			catch (System.Exception ex) // I don't think there is any way to get here, but just in case.
			{
				connection.logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod ().ToString ());
			}
			finally
			{
				connection = null;
			}
		}

		public bool hasRows ()
		{
			return connection.hasRows ();
		}

		public object getValue (string fieldName)
		{
			return connection.getValue (fieldName);
		}

		public object getValue (int fieldOrdinal)
		{
			return connection.getValue (fieldOrdinal);
		}

		public string getFieldName (int fieldOrdinal)
		{
			return connection.getFieldName (fieldOrdinal);
		}

		public string[] getFieldNames ()
		{
			return connection.getFieldNames ();
		}

		public Hashtable getNextRow ()
		{
			return connection.getNextRow ();
		}

		public List<Hashtable> getAllRows()
		{
			List<Hashtable> allRows = new List<Hashtable> ();
			Hashtable row;

			while ((row = getNextRow ()) != null)
				allRows.Add (row);

			return allRows;
		}

		public int getColumnCount ()
		{
			return connection.getColumnCount ();
		}

		public bool nextRow ()
		{
			return connection.nextRow ();
		}

		public Type getType (string fieldName)
		{
			return connection.getType (fieldName);
		}
	}
}

