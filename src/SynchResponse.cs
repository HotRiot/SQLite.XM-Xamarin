using System;

namespace SQLiteXM
{
	public class SynchResponse
	{
		public SQLiteXM.Defines.SynchErrorTypes synchErrorType;
		public ProcessingSynchError processingSynchError;
		public ExceptionSynchError exceptionSynchError;
		public bool removeFlag = true;

		public SynchResponse (bool removeFlag, SQLiteXM.Defines.SynchErrorTypes synchErrorType)
		{
			this.removeFlag = removeFlag;
			this.synchErrorType = synchErrorType;
		}

		public SynchResponse (SQLiteXM.Defines.SynchErrorTypes synchErrorType, Exception exception, string exceptionType, string exceptionMessage)
		{
			this.synchErrorType = synchErrorType;
			this.exceptionSynchError = new ExceptionSynchError (exception, exceptionType, exceptionMessage);
		}

		public SynchResponse (SQLiteXM.Defines.SynchErrorTypes  synchErrorType, int resultCode, string resultText, string resultMessage)
		{
			this.synchErrorType = synchErrorType;
			this.processingSynchError = new ProcessingSynchError (resultCode, resultText, resultMessage);
		}
	}

	public class ExceptionSynchError
	{
		public Exception exception;
		public string exceptionType;
		public string exceptionMessage;

		public ExceptionSynchError (Exception exception, string exceptionType, string exceptionMessage)
		{
			this.exception = exception; 
			this.exceptionType = exceptionType; 
			this.exceptionMessage = exceptionMessage;
		}
	}

	public class ProcessingSynchError
	{
		public int resultCode;
		public string resultText;
		public string resultMessage;

		public ProcessingSynchError (int resultCode, string resultText, string resultMessage)
		{
			this.resultCode = resultCode; 
			this.resultText = resultText; 
			this.resultMessage = resultMessage;
		}
	}

}

