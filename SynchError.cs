﻿using System;

namespace SQLiteXM
{
	public class SynchError
	{
		public SQLiteXM.Defines.SynchErrorTypes  synchErrorType;
		public string _systemSynchID;

		public ExceptionSynchError  exceptionSynchError;
		public ProcessingSynchError processingSynchError;

		public SynchError (SQLiteXM.Defines.SynchErrorTypes synchErrorType, Exception exception, string exceptionType, string exceptionMessage, string _systemSynchID)
		{
			this.synchErrorType = synchErrorType;
			this._systemSynchID = _systemSynchID;

			this.exceptionSynchError = new ExceptionSynchError (exception, exceptionType, exceptionMessage);
		}

		public SynchError (SQLiteXM.Defines.SynchErrorTypes  synchErrorType, int resultCode, string resultText, string resultMessage, string _systemSynchID)
		{
			this.synchErrorType = synchErrorType;
			this._systemSynchID = _systemSynchID;

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

