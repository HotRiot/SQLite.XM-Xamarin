using System;
using System.IO;
using System.Text;

namespace SQLiteXM
{
	public class Logging
	{
		private bool noLog;
		private int maxLogSize;
		private string logPath;
		private static readonly object synchLock = new object();

		internal Logging(string logFileName, Environment.SpecialFolder logPathSpecialFolder, int maxLogSize, bool noLog)
		{
			this.noLog = noLog;
			this.maxLogSize = maxLogSize;
			logPath = Path.Combine( Environment.GetFolderPath (logPathSpecialFolder), logFileName );
		}

		internal void log(System.Exception ex, string method, string logLevel = "Error")
		{
			if (!noLog) 
			{
				try
				{
					StringBuilder errorLogText = new StringBuilder ();
					errorLogText.AppendFormat ("Method: {0}" + Environment.NewLine, method);
					errorLogText.AppendFormat ("Exception: {0}" + Environment.NewLine, ex.ToString ());
					errorLogText.AppendFormat ("Source: {0}" + Environment.NewLine, ex.Source);

					lock (synchLock) 
					{
						File.AppendAllText (logPath, "******************************************************* " + logLevel + Environment.NewLine, Encoding.UTF8);
						File.AppendAllText (logPath, "Time Stamp: " + DateTime.UtcNow.ToString ("MM/dd/yyyy hh:mm:ss.fff tt", System.Globalization.CultureInfo.CreateSpecificCulture ("en-US")) + Environment.NewLine, Encoding.UTF8);
						File.AppendAllText (logPath, errorLogText.ToString () + Environment.NewLine, Encoding.UTF8);
						File.AppendAllText (logPath, "*************************************************************" + Environment.NewLine + Environment.NewLine, Encoding.UTF8);

						if ((new FileInfo (logPath)).Length > maxLogSize) 
						{
							int extOffset = logPath.LastIndexOf (".log");
							string oldLogPath = logPath.Insert (extOffset, ".old");

							File.Delete (oldLogPath);
							File.Move(logPath, oldLogPath);
						}
					}
				}
				#pragma warning disable 0168
				catch (System.Exception notUsed) {} // Don't want to throw an exception while processing an exception.
				#pragma warning restore 0168
			}
		}
	}
}
