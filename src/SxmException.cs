using System;
using Mono.Data.Sqlite;

namespace SQLiteXM
{
	public class SxmException : Exception
	{
		public SxmException()
		{
		}

		public SxmException(ErrorMessage ErrorMessage)
			: base(ErrorMessage.ErrorText)
		{
			this.Data.Add ("sxmErrorCode", ErrorMessage.ErrorID);
		}

		public SxmException(Exception inner)
			: base(inner.Message, inner)
		{
			this.Data.Add ("sxmErrorCode", ErrorMessages.error ["innerException"].ErrorID);
		}

		public SxmException(SqliteException sqliteException)
			: base(sqliteException.Message)
		{
			this.Data.Add ("sxmErrorCode", ErrorMessages.error ["SqliteException"].ErrorID);
			this.Data.Add ("sqliteErrorCode", sqliteException.ErrorCode);
		}

		public static Exception getInnermostException (Exception ex)
		{
			Exception iEX = ex;

			while (iEX.InnerException != null) 
				iEX = ex.InnerException;

			return iEX;
		}
	}
}


