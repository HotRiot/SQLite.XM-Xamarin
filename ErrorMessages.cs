using System;
using System.Collections.Specialized;
using System.Collections;
using System.Collections.Generic;

namespace SQLiteXM
{
	public class ErrorMessages
	{
		public static readonly Dictionary<string, ErrorMessage> error = new Dictionary<string, ErrorMessage> ();

		static ErrorMessages ()
		{
			error.Add ("missingSQL", new ErrorMessage ("Missing SQL Query.", 
				Defines.SxmErrorCode.missingSQL));
			error.Add ("lockDB", new ErrorMessage ("Unable to lock connection to the database: '{0}'.", 
				Defines.SxmErrorCode.lockDB));
			error.Add ("dbDescriptorExists", new ErrorMessage ("A descriptor already exists for the database: '{0}'.", 
				Defines.SxmErrorCode.dbDescriptorExists));
			error.Add ("noDBDescriptorExists", new ErrorMessage ("A descriptor could not be found for the database: '{0}'.", 
				Defines.SxmErrorCode.noDBDescriptorExists));
			error.Add ("invalidTableName", new ErrorMessage ("The table name '{0}' is invalid.", 
				Defines.SxmErrorCode.invalidTableName));
			error.Add ("noDatabaseExists", new ErrorMessage ("The database '{0}' does not exist.", 
				Defines.SxmErrorCode.noDatabaseExists));
			error.Add ("missingSQLStatementHeader", new ErrorMessage ("A header in the SQL statements properties file is missing.", 
				Defines.SxmErrorCode.missingSQLStatementHeader));
			error.Add ("unknownSQLStatementHeader", new ErrorMessage ("The header '{0}' in the SQL statements properties file is invalid.", 
				Defines.SxmErrorCode.unknownSQLStatementHeader));
			error.Add ("invalidSQLStatementFile", new ErrorMessage ("The SQL statements properties file is improperly formatted.", 
				Defines.SxmErrorCode.invalidSQLStatementFile));
			error.Add ("unknownSynchCommand", new ErrorMessage ("The table synch command '{0}' is not recognized.", 
				Defines.SxmErrorCode.unknownSynchCommand));
			error.Add ("invalidSQLStatementDefinition", new ErrorMessage ("An '{0}' statement in the SQL statements properties file is improperly formatted.", 
				Defines.SxmErrorCode.invalidSQLStatementDefinition));
			error.Add ("noImplicitDBDescriptorExists", new ErrorMessage ("An implicit database descriptor could not be found.", 
				Defines.SxmErrorCode.noImplicitDBDescriptorExists));
			error.Add ("unknownErrorName", new ErrorMessage ("The error '{0}' could not be fund.", 
				Defines.SxmErrorCode.unknownErrorName));
			error.Add ("innerException", new ErrorMessage ("", // Error message from inner exception.
				Defines.SxmErrorCode.innerException));
			error.Add ("unknownSQLStatement", new ErrorMessage ("The SQL statement '{0}' could not be found in the SQL statements properties file.", 
				Defines.SxmErrorCode.unknownSQLStatement));
			error.Add ("invalidDBName", new ErrorMessage ("The database name '{0}' is not valid.", 
				Defines.SxmErrorCode.invalidDBName)); 
			error.Add ("SqliteException", new ErrorMessage ("", 
				Defines.SxmErrorCode.sqliteException)); // Error message from SQLite.
			error.Add ("userDefined", new ErrorMessage ("", 
				Defines.SxmErrorCode.userDefined)); // Error message from user.
			error.Add ("threadLockError", new ErrorMessage ("The current thread already has an active instance of SxmSTransaction.", 
				Defines.SxmErrorCode.threadLockError)); 
			error.Add ("sxmSTransactionTimeout", new ErrorMessage ("Timeout trying to acquire the SxmSTransaction lock.", 
				Defines.SxmErrorCode.sxmSTransactionTimeout)); 
		}

		public static string getErrorText (string errorName)
		{
			try
			{
				return ((ErrorMessage)ErrorMessages.error[errorName]).ErrorText;

			}
			#pragma warning disable 0168
			catch (SystemException notUsed) 
			#pragma warning restore 0168
			{
				throw new SxmException (new ErrorMessage("unknownErrorName", errorName));
			}

		}

		public static Defines.SxmErrorCode getErrorID (string errorName)
		{
			try
			{
				return ((ErrorMessage)ErrorMessages.error[errorName]).ErrorID;
			}
			#pragma warning disable 0168
			catch (SystemException notUsed) 
			#pragma warning restore 0168
			{
				throw new SxmException (new ErrorMessage("unknownErrorName", errorName));
			}
		}

		private ErrorMessages () {}
	}

	public class ErrorMessage
	{
		private Defines.SxmErrorCode errorID;
		private string errorText;

		public ErrorMessage (string errorText, Defines.SxmErrorCode errorID)
		{
			this.errorText = errorText;
			this.errorID = errorID;
		}

		public ErrorMessage (string errorName, params object[] list)
		{
			this.errorText = String.Format (ErrorMessages.error [errorName].ErrorText, list);
			this.errorID = ErrorMessages.error[errorName].ErrorID;
		}

		public Defines.SxmErrorCode ErrorID
		{
			get { return errorID; }
		}
		public string ErrorText
		{
			get { return errorText; }
		}
	}
}

