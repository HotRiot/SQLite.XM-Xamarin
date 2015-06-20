using System;
using System.IO;
using Mono.Data.Sqlite;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

namespace SQLiteXM
{
	public class DatabaseDescriptor
	{
		private static Hashtable dbDescriptors = new Hashtable ();

		// Database settings.
		private string databaseName; // Required.
		public string DatabaseName
		{
			get { return databaseName; }
		}
		private Environment.SpecialFolder databaseFolder; // Optional. Default: Environment.SpecialFolder.ApplicationData.
		public Environment.SpecialFolder DatabaseFolder
		{
			get { return databaseFolder; }
		}

		// Logging settings.
		public string logfileName; // Optional. Default: Same as database name with .log extension.
		public int logfileMaxSize = 1024 * 1024; // Optional. Default: 1MB.
		public Environment.SpecialFolder logfileFolder = Environment.SpecialFolder.Personal; // Optional. Default: Environment.SpecialFolder.Personal.
		public bool noLog = false;

		public DatabaseDescriptor (string databaseName, Environment.SpecialFolder databaseFolder = Environment.SpecialFolder.ApplicationData)
		{
			try
			{
				lock (dbDescriptors.SyncRoot) 
				{
					validateDBName (databaseName);
					if (DatabaseDescriptor.getDescriptor (databaseName) != null) //Trying to create a descriptor for a database that already has a descriptor.
						return;

					this.databaseFolder = databaseFolder;
					this.databaseName = databaseName;
					logfileName = databaseName + ".log";

					createDB ();
					dbDescriptors.Add (databaseName, this);
				}
			}
			catch (System.Exception ex) 
			{
				throw new SxmException (ex);
			}
		}

		// Sanity check the database name.
		private void validateDBName (string databaseName)
		{
			if (databaseName == null || databaseName == string.Empty || databaseName.ToLower ().Equals ("main") == true || databaseName.ToLower ().Equals ("temp") == true)
				throw new SxmException (new ErrorMessage ("invalidDBName", databaseName));
		}

		private void createDB()
		{
			string databaseFolderString = Environment.GetFolderPath (databaseFolder);

			if (Directory.Exists (databaseFolderString) == false)
				Directory.CreateDirectory (databaseFolderString);

			string pathToDatabase = Path.Combine (databaseFolderString, databaseName);
			if (File.Exists (pathToDatabase) == false)
				SqliteConnection.CreateFile (pathToDatabase);
		}

		public static DatabaseDescriptor getDescriptor (string dbName)
		{
			lock (dbDescriptors.SyncRoot) 
			{
				return dbDescriptors [dbName] as DatabaseDescriptor;
			}
		}

		public static ArrayList getDatabaseNames ()
		{
			ArrayList dbNames = new ArrayList ();

			lock (dbDescriptors.SyncRoot) 
			{
				ICollection keys = dbDescriptors.Keys;
				IEnumerator keysEnumerator = keys.GetEnumerator ();
				while (keysEnumerator.MoveNext () == true)
					dbNames.Add (keysEnumerator.Current as string);
			}

			return dbNames;
		}

		public void lockDescriptor ()
		{
			Monitor.Enter (this); 
		}

		public void unlockDescriptor ()
		{
			if (Monitor.IsEntered (this) == true)
				Monitor.Exit (this);
		}

		public override string ToString()
		{
			return databaseName;
		}
	}
}

