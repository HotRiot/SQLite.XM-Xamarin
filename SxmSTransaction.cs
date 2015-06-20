using System;
using System.IO;
using Mono.Data.Sqlite;
using System.Collections;
using System.Threading.Tasks;
using System.Threading;

namespace SQLiteXM
{
	public class SxmSTransaction : SxmTransaction
	{
		private static Object serialLock = new Object ();
		bool disposed = false;

		public SxmSTransaction (SxmConnection connection, int lockWait = Timeout.Infinite)
			: base(connection)
		{
			if (Monitor.IsEntered (serialLock) == true) // Trying to allocate another SxmSTransaction on the same thread.
				throw new SxmException (ErrorMessages.error["threadLockError"]);

			if (Monitor.TryEnter (serialLock, lockWait) == false)
				throw new SxmException (ErrorMessages.error["sxmSTransactionTimeout"]);
		}

		public SxmSTransaction (string databaseName = null, int lockWait = Timeout.Infinite)
			: base(databaseName)
		{
			if (Monitor.IsEntered (serialLock) == true)
				throw new SxmException (ErrorMessages.error["threadLockError"]);

			if (Monitor.TryEnter (serialLock, lockWait) == false)
				throw new SxmException (ErrorMessages.error["sxmSTransactionTimeout"]);
		}

		protected override void Dispose (bool disposing)
		{
			if (disposed == true)
				return;

			if (Monitor.IsEntered (serialLock) == true)
				Monitor.Exit (serialLock);

			disposed = true;
			base.Dispose (disposing);
		}
	}
}

