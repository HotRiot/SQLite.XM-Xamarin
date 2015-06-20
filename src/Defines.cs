using System;

namespace SQLiteXM
{
	public class Defines
	{
		public static readonly int ONE_MINUTE = 60000; // One Minutes in milliseconds.

		// Delimeters used for enclosing commands in the SqlStatemets properties file.
		internal static readonly char openStatementDelimeter = '[';
		internal static readonly char closeStatementDelimeter = ']';

		// Cloud synch status flags for tables.
		public static readonly int NO_CLOUD_SYNCH = 0;
		public static readonly int CLOUD_SYNCH = 1;
		public static readonly int CLOUD_MOVE = 2;

		// Transaction commit / rollback flags. 
		public static readonly bool commitTransaction = true;
		public static readonly bool rollbackTransaction = false;

		// Synchronization error types. 
		public enum SynchErrorTypes{
			exception,
			processing
		};

		// System SQL commands.
/*		public static readonly string _systemInsertCloudSynchDescriptor = "INSERT INTO _systemCloudSynchDescriptor (dbName, tableName, cloudSynchFlag) VALUES(?, ?, ?)";
		public static readonly string _systemGetTables = "SELECT name FROM sqlite_master WHERE type='table'";
		public static readonly string _systemGetLastInsertID = "select last_insert_rowid() as rowID";
		public static readonly string _systemUpdateSynchID = "UPDATE {0} SET _systemSynchID = ? WHERE id = ?";
*/
		// Error message defines.
		public enum SxmErrorCode{
		sqliteException,
		innerException,
		missingSQL,
		lockDB,
		dbDescriptorExists,
		noDBDescriptorExists,
		invalidTableName,
		noDatabaseExists,
		missingSQLStatementHeader,
		unknownSQLStatementHeader,
		invalidSQLStatementFile,
		unknownSynchCommand,
		invalidSQLStatementDefinition,
		noImplicitDBDescriptorExists,
		unknownErrorName,
		unknownSQLStatement,
		invalidDBName,
		userDefined,
		threadLockError,
		sxmSTransactionTimeout
		};

		private Defines () {}
	}
}

