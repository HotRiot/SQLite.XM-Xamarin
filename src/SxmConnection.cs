using System;
using System.IO;
using System.Threading;
using Mono.Data.Sqlite;
using System.Data.Common;
using System.Collections;
using System.Threading.Tasks;

namespace SQLiteXM
{
	public class SxmConnection
	{
		public Logging logger;
		private bool transient;
		public bool Transient
		{
			get { return transient; }
		}
		private string databaseName;
		public string DatabaseName
		{
			get { return databaseName; }
		}
		private DbCommand connCommand;
		private SqliteConnection dbConn;
		private string databaseFolderPath;
		private DbDataReader connDataReader;
		private SqliteTransaction dbConnTransaction;

		static private string implicitDatabaseName;

		public SxmConnection (string databaseName, bool transient = false)
		{
			Environment.SpecialFolder logfileFolder;
			string logfileName;
			int logfileMaxSize;
			bool noLog;

			if (databaseName == null)
			{
				if (SxmConnection.implicitDatabaseName == null) 
				{
					ArrayList dbNames = DatabaseDescriptor.getDatabaseNames ();
					if (dbNames.Count != 1) // There must be only one descriptor in order to use implicit database naming.
						throw new SxmException (ErrorMessages.error["noImplicitDBDescriptorExists"]);
					else
						SxmConnection.implicitDatabaseName = dbNames [0] as string;
				} 

				databaseName = SxmConnection.implicitDatabaseName;
			}

			DatabaseDescriptor databaseDescriptor = DatabaseDescriptor.getDescriptor (databaseName);
			if (databaseDescriptor == null) 
			{
				throw new SxmException (new ErrorMessage("noDBDescriptorExists", databaseName));
			}

			this.transient = transient;
			this.databaseName = databaseName;
			databaseFolderPath = Environment.GetFolderPath (databaseDescriptor.DatabaseFolder);
			try  // We use a try-catch for the finally block. The unlock must happen.
			{
				// The descriptor is locked in order to guarantee that it is not in an indeterminate state.
				databaseDescriptor.lockDescriptor ();
				logfileName = databaseDescriptor.logfileName;
				logfileFolder = databaseDescriptor.logfileFolder;
				logfileMaxSize = databaseDescriptor.logfileMaxSize;
				noLog = databaseDescriptor.noLog;
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
				// Unlock the descriptor as soon as possible.
				databaseDescriptor.unlockDescriptor ();
			}

			logger = new Logging (logfileName, logfileFolder, logfileMaxSize, noLog );
			createNewConnection ();
		}

		private void createNewConnection ()
		{
			try
			{
				string pathToDatabase = Path.Combine( databaseFolderPath, databaseName );
				string connectionString = String.Format("Data Source={0};Version=3;", pathToDatabase);
				dbConn = new SqliteConnection (connectionString);
				dbConn.Open();
			}
			catch (SxmException ex)
			{
				throw ex;
			}
			catch (System.Exception ex) 
			{
				destroyConnection ();
				logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod ().ToString ());
				throw new SxmException (ex);
			}
		}

		public bool lockConnection (int wait = 100)
		{
			if (dbConn != null)
				if (Monitor.TryEnter (dbConn, wait) == true) 
				{
					if (dbConn.State == System.Data.ConnectionState.Broken) 
					{
						dbConn.Close ();
						dbConn.Open ();
					}

					return true;
				} 

			return false;
		}

		// Returns error code for SqliteException, otherwise throw the exception.
		public SQLiteErrorCode finishTransaction (bool commitFlag)
		{
			SQLiteErrorCode sqLiteErrorCode = SQLiteErrorCode.Ok; 

			if (dbConn != null)
				if (dbConnTransaction != null) 
					sqLiteErrorCode = doCommit (commitFlag);

			return sqLiteErrorCode;
		}

		// No-throw guarantee. Makes every effort to perform clean-up.
		public void releaseConnection (bool destroy = false)
		{
			if (dbConn != null)
			{
				try
				{
					if (dbConnTransaction != null) 
						doCommit (SQLiteXM.Defines.rollbackTransaction);
				}
				#pragma warning disable 0168
				catch( System.Exception notUsed) {} // Within a handled exception a finally is guaranteed to run. 
				#pragma warning restore 0168        // https://msdn.microsoft.com/en-us/library/zwc8s4fz.aspx   
				finally
				{
					try
					{
						if (Monitor.IsEntered (dbConn) == true)
							Monitor.Exit (dbConn);

						if (transient == true || destroy == true)
							destroyConnection ();
						else
							releaseConnectionResources ();
					}
					#pragma warning disable 0168
					catch( System.Exception notUsed) {}
					#pragma warning restore 0168
				}
			}
		}

		// Returns error code for SqliteException, otherwise throw the exception.
		private SQLiteErrorCode doCommit (bool commitFlag)
		{
			SQLiteErrorCode sqLiteErrorCode = SQLiteErrorCode.Ok;

			if (dbConnTransaction != null) 
			{
				try 
				{
					if (commitFlag == SQLiteXM.Defines.commitTransaction)
						dbConnTransaction.Commit ();
					else
						dbConnTransaction.Rollback ();

					dbConnTransaction = null;
				} 
				catch (SqliteException ex) 
				{
					logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod ().ToString ());
					//if (ex.ErrorCode == SQLiteErrorCode.Busy) {/* May do something here.*/}

					if (commitFlag == SQLiteXM.Defines.commitTransaction)
						sqLiteErrorCode = ex.ErrorCode;
					else
						throw new SxmException (ex);
				}
				catch (System.Exception ex)
				{
					logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod ().ToString ());
					throw new SxmException (ex);
				}
			}

			return sqLiteErrorCode;
		}

		public void destroyConnection ()
		{
			if (dbConn != null) 
			{
				releaseConnectionResources ();

				dbConn.Close ();
				dbConn.Dispose ();
				dbConn = null;
			}
		}

		private void releaseConnectionResources ()
		{
			if (connCommand != null) 
			{
				releaseDataReader ();
				connCommand.Dispose ();
				connCommand = null;
			}
		}

		private void releaseDataReader ()
		{
			if (connDataReader != null && connDataReader.IsClosed == false) 
			{
				connDataReader.Close ();
				connDataReader = null;
			}
		}

		public void executeQuery (string command, ArrayList parameterValues)
		{
			if (string.IsNullOrEmpty (command) == true)
				throw new SxmException (ErrorMessages.error ["missingSQL"]);

			try
			{
				if (connCommand == null)
					connCommand = dbConn.CreateCommand ();
				else
					releaseDataReader();

				connCommand.CommandText = command;
				connCommand.CommandType = System.Data.CommandType.Text;
				addCommandParameters (parameterValues);
				connDataReader = connCommand.ExecuteReader ();
			}
			catch (SxmException ex)
			{
				throw ex;
			}
			catch (System.Exception ex) 
			{
				logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod ().ToString ());
				throw new SxmException (ex);
			}
		}

		public void executeNonQuery (string command, ArrayList parameterValues)
		{
			if (string.IsNullOrEmpty (command) == true)
				throw new SxmException (ErrorMessages.error ["missingSQL"]);

			try
			{
				if (connCommand == null)
					connCommand = dbConn.CreateCommand ();
				else
					if( command.StartsWith("DELETE FROM companyReg WHERE companyRegPK") == false)
					releaseDataReader();

				connCommand.CommandText = command;
				connCommand.CommandType = System.Data.CommandType.Text;
				addCommandParameters (parameterValues);
				connCommand.ExecuteNonQuery ();
			}
			catch (SxmException ex)
			{
				throw ex;
			}
			catch (System.Exception ex) 
			{
				logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod ().ToString ());
				throw new SxmException (ex);
			}
		}

		private void addCommandParameters (ArrayList parameterValues)
		{
			connCommand.Parameters.Clear ();
			if (parameterValues != null)
				foreach (Object parameterValue in parameterValues) 
				{
					DbParameter dbParameter = connCommand.CreateParameter ();
					dbParameter.Value = parameterValue;
					connCommand.Parameters.Add (dbParameter);
				}
		}

		public void beginTransaction ()
		{
			try
			{
				if (dbConnTransaction == null)
					dbConnTransaction = dbConn.BeginTransaction ();
			}
			catch (SqliteException ex) 
			{
				if (ex.ErrorCode == SQLiteErrorCode.Busy) 
				{
					logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod ().ToString ());
				} 
				throw new SxmException (ex);
			}
		}

		public bool hasRows ()
		{
			if (connDataReader != null) 
				return connDataReader.HasRows;

			return false;
		}

		public object getValue (string fieldName)
		{
			try
			{
				if (hasRows () == true) 
				{
					int ordinal = connDataReader.GetOrdinal (fieldName);
					if (ordinal != -1)
						return connDataReader.GetValue (ordinal);
				}
			}
			catch (System.Exception ex) 
			{
				throw new SxmException (ex);
			}

			return null;
		}

		public object getValue (int fieldOrdinal)
		{
			try
			{
				if (hasRows () == true) 
					return connDataReader.GetValue (fieldOrdinal);
			}
			catch (System.Exception ex) 
			{
				throw new SxmException (ex);
			}

			return null;
		}

		public string getFieldName (int fieldOrdinal)
		{
			try
			{
				if (hasRows () == true) 
					return connDataReader.GetName (fieldOrdinal);
			}
			catch (System.Exception ex) 
			{
				throw new SxmException (ex);
			}

			return null;
		}

		public string[] getFieldNames ()
		{
			string[] fieldNames;

			if (hasRows () == true) 
			{
				fieldNames = new string[connDataReader.FieldCount];
				for (int i=0; i<connDataReader.FieldCount; i++)
					fieldNames[i] = connDataReader.GetName (i);
			}
			else
				fieldNames = new string[0];

			return fieldNames;
		}

		public Hashtable getNextRow ()
		{
			Hashtable row = null;

			if (nextRow () == true) 
			{
				row = new Hashtable ();
				int numColumns = getColumnCount ();
				for (int i=0; i<numColumns; i++) 
					row.Add (connDataReader.GetName (i), connDataReader.GetValue (i));
			}

			return row;
		}

		public int getColumnCount ()
		{
			if (hasRows () == true) 
				return connDataReader.FieldCount;

			return 0;
		}

		public bool nextRow ()
		{
			bool anotherRow = false;

			if (hasRows () == true) 
			{
				anotherRow = connDataReader.Read ();
				if (anotherRow == false)
					releaseDataReader ();
			}

			return anotherRow;
		}

		public Type getType (string fieldName)
		{
			try
			{
				if (hasRows () == true) 
				{
					int ordinal = connDataReader.GetOrdinal (fieldName);
					return connDataReader.GetFieldType (ordinal);
				}
			}
			catch (System.Exception ex) 
			{
				throw new SxmException (ex);
			}

			return null;
		}
	}
}

