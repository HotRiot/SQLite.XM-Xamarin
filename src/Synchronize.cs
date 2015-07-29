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
	public delegate Task<SynchResponse> SynchDel (List<SynchDescriptor> sdList);
	public delegate Task SynchErrorDel (SynchDescriptor synchDescriptor, SynchResponse synchResponse);
	public delegate Task SynchPostProcessDel (SynchDescriptor originalSD, List<SynchDescriptor> sdList, SynchResponse synchResponse);
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

		public bool getSynchMonitor(int millisecondsTimeout)
		{
			return Monitor.TryEnter (synchLock, millisecondsTimeout);
		}

		public void releaseSynchMonitor()
		{
			if (Monitor.IsEntered (synchLock) == true)
				Monitor.Exit (synchLock);
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
			Hashtable recordToSynch = null;

			while (true) 
			{
				try
				{
					int waitDuration = Timeout.Infinite;
					recordToSynch = getNextRecordToSynch ();

					do
					{
						getSynchMonitor (Timeout.Infinite);
						try
						{
							if (recordToSynch != null && recordToSynch.Count > 0) 
							{
								string tableName = (string)recordToSynch ["tableName"];
								if (tableName != null && descriptorRows.ContainsKey(tableName) == true)
								{
									Hashtable descriptorRow = descriptorRows [tableName];
									if ((long)descriptorRow ["cloudSynchFlag"] != Defines.NO_CLOUD_SYNCH) 
									{
										Hashtable recordDataToSynch = getRecordDataToSynch ((string)recordToSynch ["dbName"], (string)recordToSynch ["tableName"], (string)recordToSynch ["systemSynchID"]);
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

											SynchResponse synchResponse = await Synchronize.synchSettings.SynchDel (synchDescriptors);
											if (synchResponse.removeFlag == true)
											{
												// Success or a non-recoverable error. Must inspect return synchResponse.
												removeRecordFromSynch ((long)recordToSynch ["id"]);
											}
											else
											{
												// Recoverable error was encountered.
												recordToSynch = null;
												waitDuration = Defines.ONE_MINUTE * 2;
											}

											if (Synchronize.synchSettings.SynchPostProcessDel != null) // Post-process the record that was synchronized.
												await Synchronize.synchSettings.SynchPostProcessDel (synchDescriptor, synchDescriptors, synchResponse);
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
						}
						finally
						{
							releaseSynchMonitor ();  // Release the monitor between each record synch in order to allow waiting operations to proceed.
						}
					}while (recordToSynch != null && recordToSynch.Count > 0);

					synchLock.WaitOne (waitDuration);
					synchLock.Reset ();
				}
				catch (SxmException ex) 
				{
					Defines.SxmErrorCode sxmErrorCode = (Defines.SxmErrorCode)ex.Data ["sxmErrorCode"];

					if (sxmErrorCode == Defines.SxmErrorCode.sxmSTransactionTimeout != true && sxmErrorCode == Defines.SxmErrorCode.lockDB != true) 
					{
						if (sxmErrorCode == Defines.SxmErrorCode.sqliteException == true && ex.Data.Contains ("sqliteErrorCode") == true) 
						{
							SQLiteErrorCode sqliteErrorCode = (SQLiteErrorCode)ex.Data ["sqliteErrorCode"];
							if (sqliteErrorCode != SQLiteErrorCode.Busy && sqliteErrorCode != SQLiteErrorCode.Locked)
								removeRecordFromSynch ((long)recordToSynch ["id"]); 
						} 
						else
							removeRecordFromSynch ((long)recordToSynch ["id"]); 
					}
				}
				#pragma warning disable 0168
				catch (Exception notUsed) 
				#pragma warning restore 0168
				{ 
					removeRecordFromSynch ((long)recordToSynch ["id"]); 
				}
			}
		}

		private Hashtable getRecordDataToSynch (string dbName, string tableName, string systemSynchID)
		{
			Hashtable row = null;

			ArrayList paramValues = new ArrayList ();
			paramValues.Add (systemSynchID);

			using (SxmTransaction sxmTransaction = new SxmTransaction (dbName))
			{
				sxmTransaction.executeQueryDirect (string.Format ("SELECT * FROM {0} WHERE systemSynchID = ?", tableName), paramValues);
				row = sxmTransaction.getNextRow ();
			}

			return row;
		}

		private Hashtable getNextRecordToSynch ()
		{
			Hashtable row = null;

			try
			{
				using (SxmTransaction sxmTransaction = new SxmTransaction (dbName))
				{
					sxmTransaction.executeQueryDirect ("SELECT * FROM _systemCloudSynch LIMIT 1", null);
					row = sxmTransaction.getNextRow ();
				}
			}
			#pragma warning disable 0168
			catch (Exception doNothing) { /* If an error occurs reading the synch log, just wait to try again. */ }
			#pragma warning restore 0168

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

		internal static async Task<SynchResponse> SynchDel (List<SynchDescriptor> sdList)
		{
			SynchDescriptor synchDescriptor = null;

			for (int countProcessed=0; countProcessed<sdList.Count; countProcessed++) 
			{
				synchDescriptor = sdList [countProcessed];

				if (synchDescriptor.action.Equals ("insert") == true || 
					synchDescriptor.action.Equals ("update") == true) 
						synchDescriptor.synchResponse = await synchData (synchDescriptor);

				if (synchDescriptor.action.Equals ("delete") == true)
					synchDescriptor.synchResponse = await synchDelData (synchDescriptor);

				// There was an error and it is non-recoverable.
				if (synchDescriptor.synchResponse.synchErrorType != SQLiteXM.Defines.SynchErrorTypes.success &&
					synchDescriptor.synchResponse.removeFlag == true) 
				{
					await performPartialSynchCleanup (sdList, synchDescriptor.synchResponse, countProcessed);
					break;
				}

				// There was an error and it is recoverable. Don't delete record from relay log.
				if (synchDescriptor.synchResponse.synchErrorType != SQLiteXM.Defines.SynchErrorTypes.success &&
					synchDescriptor.synchResponse.removeFlag == false)
						break; 
			}

			return synchDescriptor.synchResponse;
		}

		private static async Task performPartialSynchCleanup (List<SynchDescriptor> sdList, SynchResponse synchResponse, int countProcessed)
		{
			// If delete record from relay log with error, then remove any already inserted records.
			for (int i = 0; i <= countProcessed; ++i) 
			{
				SynchDescriptor processedSD = sdList [i];
				if (processedSD.action.Equals ("insert") == true) 
				{
					SynchResponse cleanupSynchResponse = await synchDelData (processedSD);
					if (cleanupSynchResponse.synchErrorType != SQLiteXM.Defines.SynchErrorTypes.success)
					{
						if (cleanupSynchResponse.removeFlag == true) // Non-recoverable.
							break;
						else 
						{
							synchResponse.removeFlag = false;
							break;
						}
					}
				}
			}
		}

		#pragma warning disable 1998
		internal static async Task ErrorDel(SynchDescriptor synchDescriptor, SynchResponse synchResponse)
		#pragma warning restore 1998
		{
			if (synchResponse.synchErrorType == SQLiteXM.Defines.SynchErrorTypes.exception ) 
			{
				if (synchResponse.exceptionSynchError.exceptionType.Equals ("WebException") == true || 
					synchResponse.exceptionSynchError.exceptionType.Equals ("IOException")  == true || 
					synchResponse.exceptionSynchError.exceptionType.Equals ("OutOfMemoryException") == true)
						synchResponse.removeFlag = false;
			}
			else
				if (synchResponse.synchErrorType == SQLiteXM.Defines.SynchErrorTypes.processing) 
				{
					if (synchResponse.processingSynchError.resultCode == HotRiot.DB_FULL_EXCEPTION)
						synchResponse.removeFlag = false;
				}

			// This could be used in order to avoid using 'async'.
			// But it's easy enough to use the pragma to quiet 
			// the compiler and no harm from inclusion of async.
			/*var tcs = new TaskCompletionSource<bool>();
			tcs.SetResult(rc);			
			return tcs.Task;*/
		}

		private static async Task<SynchResponse> synchData (SynchDescriptor synchDescriptor)
		{
			HRInsertResponse hrInsertResponse = null;
			string errorMessage = string.Empty;
			SynchResponse synchResponse = null;
			String fieldName = null;

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
				synchResponse = new SynchResponse (SQLiteXM.Defines.SynchErrorTypes.exception, hex.InnerException, (string)hex.Data ["exceptionType"], hex.Message);
			}

			if (synchResponse == null) 
			{
				NameValueCollection recordData = new NameValueCollection ();
				NameValueCollection fileData = new NameValueCollection ();

				ArrayList fileFields = null;
				tableFileFields.TryGetValue (synchDescriptor.dbName + synchDescriptor.tableName, out fileFields);
				recordData.Add ("systemSynchID", synchDescriptor.recordDataToSynch ["systemSynchID"].ToString ());

				foreach (DictionaryEntry pair in synchDescriptor.recordDataToSynch) 
				{
					if ((fieldName = pair.Key.ToString ()).Equals ("systemSynchID") == false) 
					{
						if (fileFields != null && fileFields.Count > 0) 
						{
							bool fileFound = false;
							foreach (string fielFieldName in fileFields) 
							{
								if (fielFieldName.Equals (fieldName) == true) 
								{  // This is a File field.
									string filePath = pair.Value.ToString ();

									if (filePath != null && filePath != string.Empty)
										fileData.Add (fieldName, filePath);

									fileFound = true;
									break;
								}
							}

							if (fileFound == false)
								recordData.Add (fieldName, pair.Value.ToString ());
						} else
							recordData.Add (fieldName, pair.Value.ToString ());
					}
				}
			
				try 
				{
					if ((hrInsertResponse = await HotRiot.getHotRiotInstance.submitKeyUpdateInsertRecord (synchDescriptor.tableName, "systemSynchID", recordData, fileData)).getResultCode () == HotRiot.SUCCESS) {
						foreach (string fielFieldName in fileFields) 
						{
							if (fileData [fielFieldName] == null) 
							{
								try 
								{
									hrInsertResponse = await HotRiot.getHotRiotInstance.keyDeleteFile (synchDescriptor.tableName, "systemSynchID", recordData ["systemSynchID"], fielFieldName);
								}
								#pragma warning disable 0168
								catch (Exception notUsed) {}
								#pragma warning restore 0168
							}
						}
					}
				} 
				catch (HotRiotException hex) 
				{
					synchResponse = new SynchResponse (SQLiteXM.Defines.SynchErrorTypes.exception, hex.InnerException, (string)hex.Data ["exceptionType"], hex.Message);
				}

				if (hrInsertResponse != null) 
				{
					if (hrInsertResponse.getResultCode () != HotRiot.SUCCESS) 
					{
						ResultDetails rd = hrInsertResponse.getResultDetails ();
						synchResponse = new SynchResponse (SQLiteXM.Defines.SynchErrorTypes.processing, rd.ResultCode, rd.ResultText, rd.ResultMessage);
					}
				}
			}

			if (synchResponse != null)
				await Synchronize.synchSettings.SynchErrorDel (synchDescriptor, synchResponse);
			else
				synchResponse = new SynchResponse (true, SQLiteXM.Defines.SynchErrorTypes.success);
							
			return synchResponse;
		}

		private static async Task<SynchResponse> synchDelData (SynchDescriptor synchDescriptor)
		{
			HRDeleteResponse hrDeleteResponse = null;
			string errorMessage = string.Empty;
			SynchResponse synchResponse = null;

			NameValueCollection recordData = new NameValueCollection();
			foreach (DictionaryEntry pair in synchDescriptor.recordDataToSynch) 
			{
				if (pair.Key.ToString ().Equals ("systemSynchID") == true) 
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
				synchResponse = new SynchResponse (SQLiteXM.Defines.SynchErrorTypes.exception, hex.InnerException, (string)hex.Data ["exceptionType"], hex.Message);
			}

			if (hrDeleteResponse != null)
				if (hrDeleteResponse.getResultCode () != HotRiot.SUCCESS) 
				{
					ResultDetails rd = hrDeleteResponse.getResultDetails ();
					synchResponse = new SynchResponse (SQLiteXM.Defines.SynchErrorTypes.processing, rd.ResultCode, rd.ResultText, rd.ResultMessage);
				}

			if (synchResponse != null)
				await Synchronize.synchSettings.SynchErrorDel (synchDescriptor, synchResponse);
			else
				synchResponse = new SynchResponse (true, SQLiteXM.Defines.SynchErrorTypes.success);

			return synchResponse;
		}
	}
}

