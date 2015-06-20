using System;
using HotRiot_CS;
using Mono.Data.Sqlite;
using System.Threading;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace SQLiteXM
{
	public delegate Task<bool> SynchDel (List<SynchDescriptor> sdl);
	public delegate Task<bool> SynchErrorDel (SynchError synchError);
	public delegate Task SynchPostProcessDel (List<SynchDescriptor> sdl);
	public delegate Task<List<SynchDescriptor>> SynchPreProcessDel (SynchDescriptor sd);

	public class Synchronize
	{
		private string dbName;
		private string hrAppName;
		private Thread synchThread; 
		private static HotRiot hotRiot;
		private readonly EventWaitHandle synchLock;
		private static SynchSettings synchSettings;
		private Dictionary<string, Hashtable> descriptorRows = new Dictionary<string, Hashtable> ();
		private static Dictionary<string, Object> synchDescriptors = new Dictionary<string, Object> ();
		private static Dictionary<string, ArrayList> tableFileFields = new Dictionary<string, ArrayList>();

		public static Synchronize createSynchronize (SxmConnection sxmConnection, string hrAppName, SynchSettings synchSettings)
		{
			Synchronize synchronize = null;

			try
			{
				if (synchDescriptors.ContainsKey (sxmConnection.DatabaseName) == false)
					synchronize = new Synchronize (sxmConnection, hrAppName, synchSettings);

				if (hrAppName != null && hotRiot == null)
					hotRiot = HotRiot.init(hrAppName);
			}
			catch (System.Exception ex)
			{
				sxmConnection.logger.log (ex, System.Reflection.MethodBase.GetCurrentMethod ().ToString ());
			}

			return synchronize;
		}

		public void interruptSynchThread()
		{
			synchLock.Set ();
		}

		private Synchronize (SxmConnection sxmConnection, string hrAppName, SynchSettings synchSettings)
		{
			Synchronize.synchSettings = synchSettings;
			this.hrAppName = hrAppName;

			dbName = sxmConnection.DatabaseName;
			List<Hashtable> synchDescriptorList = getSynchDescriptors (sxmConnection);
			synchThread = parseSynchDescriptors (synchDescriptorList);

			if (synchThread != null) 
			{
				synchLock = new EventWaitHandle (false, EventResetMode.ManualReset);
				synchThread.Start ();
			}
			else 
			{
				descriptorRows.Clear ();

				dbName = null;
				hrAppName = null;
				descriptorRows = null;
			}
		}

		private List<Hashtable> getSynchDescriptors (SxmConnection sxmConnection)
		{
			List<Hashtable> synchDescriptorList = null;
			try
			{
				using (SxmTransaction sxmTransaction = new SxmTransaction (sxmConnection))
				{
					sxmTransaction.executeQueryDirect ("SELECT * FROM _systemCloudSynchDescriptor", null);
					synchDescriptorList = sxmTransaction.getAllRows ();
				}

				synchDescriptors.Add (sxmConnection.DatabaseName, new Object());
			}
			#pragma warning disable 0168
			catch (System.Exception notUsed) { /* Not much to do here. */ }
			#pragma warning restore 0168

			return synchDescriptorList;
		}

		private Thread parseSynchDescriptors (List<Hashtable> synchDescriptorList)
		{
			Thread synchThread = null;

			if (synchDescriptorList != null)
				foreach (Hashtable descriptorRow in synchDescriptorList) 
				{
					if ( (long)descriptorRow ["cloudSynchFlag"] != Defines.NO_CLOUD_SYNCH && synchThread == null) 
						synchThread = new Thread(new ThreadStart(this.startSynch));

					descriptorRows.Add ((string)descriptorRow ["tableName"], descriptorRow);
				}

			return synchThread;
		}

		private async void startSynch()
		{
			while (true) 
			{
				try
				{
					int waitDuration = Timeout.Infinite;
					Hashtable recordToSynch = getNextRecordToSynch ();

					do
					{
						if (recordToSynch != null && recordToSynch.Count > 0) 
						{
							string tableName = (string)recordToSynch ["tableName"];
							if (tableName != null && descriptorRows.ContainsKey(tableName) == true)
							{
								Hashtable descriptorRow = descriptorRows [tableName];
								if ((long)descriptorRow ["cloudSynchFlag"] != Defines.NO_CLOUD_SYNCH) 
								{
									Hashtable recordDataToSynch = getRecordDataToSynch ((string)recordToSynch ["dbName"], (string)recordToSynch ["tableName"], (string)recordToSynch ["_systemSynchID"]);
									if (recordDataToSynch != null && recordDataToSynch.Count > 0) 
									{
										List<SynchDescriptor> synchDescriptors = null;
										SynchDescriptor synchDescriptor = new SynchDescriptor ((string)recordToSynch ["action"], (string)recordToSynch ["dbName"], (string)recordToSynch ["tableName"], (Hashtable)recordDataToSynch);

										if (hrAppName == null) // Performing custom synchronization.
										{
											synchDescriptors = new List<SynchDescriptor> ();
											synchDescriptors.Add (synchDescriptor);
										}
										else 
										{
											if (Synchronize.synchSettings.SynchPreProcessDel != null) // Pre-process the record to be synchronized.
												synchDescriptors = await Synchronize.synchSettings.SynchPreProcessDel (synchDescriptor);
											else 
											{
												synchDescriptors = new List<SynchDescriptor> ();
												synchDescriptors.Add (synchDescriptor);
											}
										}
											
										if (await Synchronize.synchSettings.SynchDel (synchDescriptors) == true)
										{
											removeRecordFromSynch ((long)recordToSynch ["id"]);
											if (Synchronize.synchSettings.SynchPostProcessDel != null) // Pre-process the record that was synchronized.
												await Synchronize.synchSettings.SynchPostProcessDel (synchDescriptors);
										}
										else
										{
											recordToSynch = null;
											waitDuration = Defines.ONE_MINUTE * 2;
										}
									}
									else
										removeRecordFromSynch ((long)recordToSynch ["id"]);
								}
								else
									removeRecordFromSynch ((long)recordToSynch ["id"]);
							}
							else
								removeRecordFromSynch ((long)recordToSynch ["id"]);

							if (recordToSynch != null) // If null, a recoverable error was encountered trying to synch the record.
								recordToSynch = getNextRecordToSynch ();
						}
					}while (recordToSynch != null && recordToSynch.Count > 0);

					synchLock.WaitOne (waitDuration);
					synchLock.Reset ();
				}
				#pragma warning disable 0168
				catch (Exception notUsed) { /* We need this to ensure the synch loop. */ }
				#pragma warning restore 0168
			}
		}

		private Hashtable getRecordDataToSynch (string dbName, string tableName, string _systemSynchID)
		{
			Hashtable row = null;

			ArrayList paramValues = new ArrayList ();
			paramValues.Add (_systemSynchID);

			using (SxmTransaction sxmTransaction = new SxmTransaction (dbName))
			{
				sxmTransaction.executeQueryDirect (string.Format ("SELECT * FROM {0} WHERE _systemSynchID = ?", tableName), paramValues);
				row = sxmTransaction.getNextRow ();
			}

			return row;
		}

		private Hashtable getNextRecordToSynch ()
		{
			Hashtable row = null;

			using (SxmTransaction sxmTransaction = new SxmTransaction (dbName))
			{
				sxmTransaction.executeQueryDirect ("SELECT * FROM _systemCloudSynch LIMIT 1", null);
				row = sxmTransaction.getNextRow ();
			}

			return row;
		}

		private void removeRecordFromSynch (long id)
		{
			ArrayList paramValues = new ArrayList ();
			paramValues.Add (id);

			using (SxmTransaction sxmTransaction = new SxmTransaction (dbName))
			{
				sxmTransaction.executeSystemUpdateDirect ("DELETE FROM _systemCloudSynch WHERE id = ?", paramValues);
				sxmTransaction.commitTransaction ();
			}
		}

		public static async Task<bool> SynchDel (List<SynchDescriptor> sdList)
		{
			bool retval = true; // Delete record from relay log.

			foreach (SynchDescriptor synchDescriptor in sdList) 
			{
				if (synchDescriptor.action.Equals ("update") == true)
					retval = await synchData (synchDescriptor);

				if (synchDescriptor.action.Equals ("delete") == true)
					retval = await synchDelData (synchDescriptor);

				if (retval == false)
					break;
			}

			return retval;
		}

		#pragma warning disable 1998
		internal static async Task<bool> ErrorDel(SynchError synchError)
		#pragma warning restore 1998
		{
			bool rc = true;

			if (synchError.synchErrorType == SQLiteXM.Defines.SynchErrorTypes.exception ) 
			{
				if (synchError.exceptionSynchError.exceptionType.Equals ("WebException") == true || 
					synchError.exceptionSynchError.exceptionType.Equals ("IOException")  == true || 
					synchError.exceptionSynchError.exceptionType.Equals ("OutOfMemoryException") == true)
					    rc = false;
			}
			else
				if (synchError.synchErrorType == SQLiteXM.Defines.SynchErrorTypes.processing) 
				{
					if (synchError.processingSynchError.resultCode == HotRiot.DB_FULL_EXCEPTION)
						rc = false;
				}

			// This could be used in order to avoid using 'async'.
			// But it's easy enough to use the pragma to quiet 
			// the compiler and no harm from inclusion of async.
			/*var tcs = new TaskCompletionSource<bool>();
			tcs.SetResult(rc);			
			return tcs.Task;*/

			return rc;
		}

		private static async Task<bool> synchData (SynchDescriptor synchDescriptor)
		{
			HRInsertResponse hrInsertResponse = null;
			string errorMessage = string.Empty;
			SynchError synchError = null;
			String fieldName = null;
			bool retval = true;

			try
			{
				if (tableFileFields.ContainsKey (synchDescriptor.dbName + synchDescriptor.tableName) == false)
				{
					ArrayList fileFieldList = new ArrayList();
					HRMetadataResponse hrMetadataResponse = await HotRiot.getHotRiotInstance.submitGetMetadata(synchDescriptor.tableName);
					if (hrMetadataResponse.getResultCode() == HotRiot.SUCCESS)
					{
						string [] fieldnames = hrMetadataResponse.getFieldNames();
						string [] fieldTypes = hrMetadataResponse.getFieldTypes();

						for (int i=0; i<fieldTypes.Length; i++)
						{
							if( fieldTypes[i].Equals ("File") == true)
								fileFieldList.Add (fieldnames[i]);
						}
						tableFileFields.Add (synchDescriptor.dbName + synchDescriptor.tableName, fileFieldList);
					}
				}
			}			
			catch( HotRiotException hex )
			{
				string ieMessage = string.Empty;
				if (hex.InnerException != null)
					ieMessage = hex.InnerException.Message;

				synchError = new SynchError (SQLiteXM.Defines.SynchErrorTypes.exception, hex.InnerException, hex.Message, ieMessage, (string)synchDescriptor.recordDataToSynch ["_systemSynchID"]);
			}


			NameValueCollection recordData = new NameValueCollection();
			NameValueCollection fileData = new NameValueCollection();

			ArrayList fileFields = null;
			tableFileFields.TryGetValue (synchDescriptor.dbName + synchDescriptor.tableName, out fileFields);
			recordData.Add ("systemSynchID", synchDescriptor.recordDataToSynch ["_systemSynchID"].ToString());

			foreach (DictionaryEntry pair in synchDescriptor.recordDataToSynch) 
			{
				if ((fieldName = pair.Key.ToString ()).Equals ("_systemSynchID") == false) 
				{
					if (fileFields != null && fileFields.Count > 0) 
					{
						bool fileFound = false;
						foreach (string fielFieldName in fileFields) 
						{
							if (fielFieldName.Equals (fieldName) == true)  // This is a File field.
							{
								string filePath = pair.Value.ToString ();

								if (filePath != null && filePath != string.Empty)
									fileData.Add (fieldName, filePath);

								fileFound = true;
								break;
							}
						}

						if (fileFound == false)
							recordData.Add (fieldName, pair.Value.ToString ());
					} 
					else
						recordData.Add (fieldName, pair.Value.ToString ());
				}
			}
			
			try
			{
				if ((hrInsertResponse = await HotRiot.getHotRiotInstance.submitKeyUpdateInsertRecord (synchDescriptor.tableName, "systemSynchID", recordData, fileData)).getResultCode() == HotRiot.SUCCESS)
				{
					foreach (string fielFieldName in fileFields) 
					{
						if (fileData [fielFieldName] == null)
						{
							try
							{
								hrInsertResponse = await HotRiot.getHotRiotInstance.keyDeleteFile (synchDescriptor.tableName, "systemSynchID", recordData ["systemSynchID"], fielFieldName);
							}
							#pragma warning disable 0168
							catch( Exception notUsed) {}
							#pragma warning restore 0168
						}
					}
				}
			}			
			catch( HotRiotException hex )
			{
				string ieMessage = string.Empty;
				if (hex.InnerException != null)
					ieMessage = hex.InnerException.Message;

				synchError = new SynchError (SQLiteXM.Defines.SynchErrorTypes.exception, hex.InnerException, hex.Message, ieMessage, (string)synchDescriptor.recordDataToSynch ["_systemSynchID"]);
			}

			if (hrInsertResponse != null)
				if (hrInsertResponse.getResultCode () != HotRiot.SUCCESS) 
				{
					ResultDetails rd = hrInsertResponse.getResultDetails ();
					synchError = new SynchError (SQLiteXM.Defines.SynchErrorTypes.processing, rd.ResultCode, rd.ResultText, rd.ResultMessage, (string)synchDescriptor.recordDataToSynch ["_systemSynchID"]);
				}

			if (synchError != null)
				retval = await Synchronize.synchSettings.SynchErrorDel (synchError);
			
			return retval;
		}

		private static async Task<bool> synchDelData (SynchDescriptor synchDescriptor)
		{
			HRDeleteResponse hrDeleteResponse = null;
			string errorMessage = string.Empty;
			SynchError synchError = null;
			bool retval = true;

			NameValueCollection recordData = new NameValueCollection();
			foreach (DictionaryEntry pair in synchDescriptor.recordDataToSynch) 
			{
				if (pair.Key.ToString ().Equals ("_systemSynchID") == true) 
				{
					recordData.Add ("systemSynchID", pair.Value.ToString ());
					break;
				}
			}

			try
			{
				hrDeleteResponse = await HotRiot.getHotRiotInstance.submitKeyDeleteRecord (synchDescriptor.tableName, recordData);
			}			
			catch( HotRiotException hex )
			{
				string ieMessage = string.Empty;
				if (hex.InnerException != null)
					ieMessage = hex.InnerException.Message;

				synchError = new SynchError (SQLiteXM.Defines.SynchErrorTypes.exception, hex.InnerException, hex.Message, ieMessage, (string)synchDescriptor.recordDataToSynch ["_systemSynchID"]);
			}

			if (hrDeleteResponse != null)
			if (hrDeleteResponse.getResultCode () != 0) 
				{
				ResultDetails rd = hrDeleteResponse.getResultDetails ();
					synchError = new SynchError (SQLiteXM.Defines.SynchErrorTypes.processing, rd.ResultCode, rd.ResultText, rd.ResultMessage, (string)synchDescriptor.recordDataToSynch ["_systemSynchID"]);
				}

			if (synchError != null)
				retval = await Synchronize.synchSettings.SynchErrorDel (synchError);

			return retval;
		}
	}
}

