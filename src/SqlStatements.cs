using System;
using System.Collections;
using System.Collections.Specialized;

namespace SQLiteXM
{
	public class SqlStatements
	{
		internal static Hashtable alterStatements = null;
		internal static Hashtable indexStatements = null;
		internal static Hashtable insertStatements = new Hashtable ();
		internal static Hashtable tableCreateStatements = new Hashtable ();
		internal static NameValueCollection selectStatements = new NameValueCollection ();
		internal static NameValueCollection updateStatements = new NameValueCollection ();
		internal static NameValueCollection deleteStatements = new NameValueCollection ();

		internal static void addInsertDefinition (string insertName, string tableName, string insertSQL)
		{
			if (insertStatements.ContainsKey (insertName) == false)
				insertStatements.Add ( insertName, new InsertDefinition (tableName, insertSQL));
		}

		internal static void addSelectDefinition (string selectName, string selectSQL)
		{
			if (selectStatements [selectName] == null)
				selectStatements.Add (selectName, selectSQL);
		}

		internal static void addUpdateDefinition (string updateName, string updateSQL)
		{
			if (updateStatements [updateName] == null)
				updateStatements.Add (updateName, updateSQL);
		}

		internal static void addDeleteDefinition(string deleteName, string deleteSQL)
		{
			if (deleteStatements [deleteName] == null)
				deleteStatements.Add (deleteName, deleteSQL);
		}

		internal static void addIndexDefinition (string dbAndTableName, string indexName, string sqlStatement)
		{
			if (indexStatements == null)
				indexStatements = new Hashtable ();

			ArrayList indexStatementsList = indexStatements [dbAndTableName] as ArrayList;
			if (indexStatementsList == null) 
			{
				indexStatementsList = new ArrayList ();
				indexStatements.Add ( dbAndTableName, indexStatementsList);
			}

			indexStatementsList.Add ( new IndexDefinition (indexName, sqlStatement));
		}

		internal static void addAlterDefinition (string dbAndTableName, string columnName, string sqlStatement)
		{
			if (alterStatements == null)
				alterStatements = new Hashtable ();

			ArrayList alterStatementsList = alterStatements [dbAndTableName] as ArrayList;
			if (alterStatementsList == null) 
			{
				alterStatementsList = new ArrayList ();
				alterStatements.Add ( dbAndTableName, alterStatementsList);
			}

			alterStatementsList.Add ( new AlterDefinition (columnName, sqlStatement));
		}

		internal static void addTableDefinition (string tableName, string tableSQL, int cloudPush)
		{
			if (tableCreateStatements == null)
				tableCreateStatements = new Hashtable ();

			tableCreateStatements.Add( tableName, new TableDefinition (tableSQL, cloudPush));
		}

		internal static void clearStatementTables ()
		{
			if (alterStatements != null) 
			{
				alterStatements.Clear ();
				alterStatements = null;
			}

			tableCreateStatements.Clear ();
			tableCreateStatements = null;

			if (indexStatements != null) 
			{
				indexStatements.Clear ();
				indexStatements = null;
			}
		}

		private SqlStatements () {}
	}
}

