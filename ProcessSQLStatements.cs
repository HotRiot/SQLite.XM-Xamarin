using System;
using System.IO;

namespace SQLiteXM
{
	public class ProcessSQLStatements
	{
		private ProcessSQLStatements ()	{}

		public static bool Parse (StreamReader sqlStatementAssets)
		{
			int searchOffset = 0;

			string sqlStatements = sqlStatementAssets.ReadToEnd ();
			while ( (searchOffset = getHeader (searchOffset, sqlStatements)) != -1 ) {}

			return true;
		}

		private static int getHeader (int searchOffset, string sqlStatements)
		{
			int index = sqlStatements.IndexOf (Defines.openStatementDelimeter, searchOffset);
			if (index != -1) 
			{
				int sIndex = index+1;
				index = sqlStatements.IndexOf (Defines.closeStatementDelimeter, sIndex);
				if (index != -1) 
				{
					if (sIndex == index)
						throw new SxmException (ErrorMessages.error["missingSQLStatementHeader"]);

					string header = sqlStatements.Substring (sIndex, index-sIndex).Trim();
					index = parseHeader (header, index+1, sqlStatements);
				}
				else
					throw new SxmException (ErrorMessages.error["invalidSQLStatementFile"]);
			}

			return index;
		}

		private static int parseHeader(string header, int index, string sqlStatements)
		{
			header = header.ToLower ();

			switch (header) 
			{
				case "table":
					index = processTableStatements (index, sqlStatements);
					break;

				case "insert":
					index = processInsertStatements (index, sqlStatements);
					break;

				case "alter":
					index = processAlterStatements (index, sqlStatements);
					break;

				case "index":
					index = processIndexStatements (index, sqlStatements);
					break;

				case "select":
				case "update":
				case "delete":
					index = processStatement (index, header, sqlStatements);
					break;

				default:
					throw new SxmException (new ErrorMessage("unknownSQLStatementHeader", header));
			}

			return index;
		}

		private static int processTableStatements(int index, string sqlStatements)
		{
			CommandReturn commandReturn = null;
			string sqlStatement;
			string dbName;
			int synch;

			do {
				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // Were finished processing the table statements.
					break;
				dbName = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // The table create statement cannot be empty.
				{
					throw new SxmException (new ErrorMessage("invalidSQLStatementDefinition", "TABLE"));
				}
				sqlStatement = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // The synch statement cannot be empty.
				{
					throw new SxmException (new ErrorMessage("invalidSQLStatementDefinition", "TABLE"));
				}
				synch = parseSynchCommand (commandReturn.command.ToLower ());

				SqlStatements.addTableDefinition (dbName, sqlStatement, synch);
			} while (true);

			return index;
		}

		private static int processInsertStatements(int index, string sqlStatements)
		{
			CommandReturn commandReturn = null;
			string sqlStatement;
			string tableName;
			string dbName;

			do {
				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // Were finished processing the insert statements.
					break;
				dbName = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				tableName = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // The SQL insert statement cannot be empty.
				{
					throw new SxmException (new ErrorMessage("invalidSQLStatementDefinition", "INSERT"));
				}
				sqlStatement = commandReturn.command;

				SqlStatements.addInsertDefinition (dbName, tableName, sqlStatement);
			} while (true);

			return index;
		}

		private static int processAlterStatements(int index, string sqlStatements)
		{
			CommandReturn commandReturn = null;
			string dbAndTableName;
			string sqlStatement;
			string columnName;

			do {
				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // Were finished processing the alter statements.
					break;
				dbAndTableName = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				columnName = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // The SQL insert statement cannot be empty.
				{
					throw new SxmException (new ErrorMessage("invalidSQLStatementDefinition", "ALTER"));
				}
				sqlStatement = commandReturn.command;

				SqlStatements.addAlterDefinition (dbAndTableName, columnName, sqlStatement);
			} while (true);

			return index;
		}

		private static int processIndexStatements(int index, string sqlStatements)
		{
			CommandReturn commandReturn = null;
			string dbAndTableName;
			string sqlStatement;
			string indexName;

			do {
				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // Were finished processing the index statements.
					break;
				dbAndTableName = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				indexName = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // The SQL insert statement cannot be empty.
				{
					throw new SxmException (new ErrorMessage("invalidSQLStatementDefinition", "INDEX"));
				}
				sqlStatement = commandReturn.command;

				SqlStatements.addIndexDefinition (dbAndTableName, indexName, sqlStatement);
			} while (true);

			return index;
		}

		private static int processStatement(int index, string header, string sqlStatements)
		{
			CommandReturn commandReturn = null;
			string sqlStatement;
			string sqlName;

			do {
				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // Were finished processing the select statements.
					break;
				sqlName = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // The SQL select statement cannot be empty.
				{
					throw new SxmException (new ErrorMessage("invalidSQLStatementDefinition", header));
				}
				sqlStatement = commandReturn.command;

				if( header.Equals ("select") == true)
					SqlStatements.addSelectDefinition (sqlName, sqlStatement);
				if( header.Equals ("delete") == true)
					SqlStatements.addDeleteDefinition (sqlName, sqlStatement);
				if( header.Equals ("update") == true)
					SqlStatements.addUpdateDefinition (sqlName, sqlStatement);
			} while (true);

			return index;
		}

		private static CommandReturn getCommand (int index, string sqlStatements)
		{
			CommandReturn commandReturn = new CommandReturn ();

			index = sqlStatements.IndexOf (Defines.openStatementDelimeter, index);
			if (index != -1) 
			{
				int sIndex = index+1;
				index = sqlStatements.IndexOf (Defines.closeStatementDelimeter, sIndex);
				if (index != -1) 
				{
					if (sIndex != index)
						commandReturn.command = sqlStatements.Substring (sIndex, index - sIndex).Trim();
					else
						commandReturn.command = string.Empty;

					commandReturn.index = index+1;
				}
				else
					throw new SxmException (ErrorMessages.error["invalidSQLStatementFile"]);
			}
			else
				throw new SxmException (ErrorMessages.error["invalidSQLStatementFile"]);

			return commandReturn;
		}

		private static int parseSynchCommand (string synchCommand)
		{
			if (synchCommand.Equals ("synch") == true)
				return Defines.CLOUD_SYNCH;
			if (synchCommand.Equals ("no_synch") == true)
				return Defines.NO_CLOUD_SYNCH;
			if (synchCommand.Equals ("move") == true)
				return Defines.CLOUD_MOVE;

			throw new SxmException (new ErrorMessage("unknownSynchCommand", synchCommand));
		}

		class CommandReturn
		{
			public int index; 
			public string command;

			public CommandReturn()
			{
			}
		}
	}

}

