using System;

namespace SQLiteXM
{
	public class SynchOptions
	{
		private Synchronize.SynchErrorDel synchErrorDel;
		public Synchronize.SynchErrorDel SynchErrorDel
		{
			get { return synchErrorDel; }
			set { synchErrorDel = value; }
		}

		private Synchronize.SynchPreProcessDel synchPreProcessDel;
		public Synchronize.SynchPreProcessDel SynchPreProcessDel
		{
			get { return synchPreProcessDel; }
			set { synchPreProcessDel = value; }
		}

		private Synchronize.SynchPostProcessDel synchPostProcessDel;
		public Synchronize.SynchPostProcessDel SynchPostProcessDel
		{
			get { return synchPostProcessDel; }
			set { synchPostProcessDel = value; }
		}

		public SynchOptions ()
		{
		}
		public SynchOptions (SynchOptions synchOptions)
		{
			if (synchOptions != null) 
			{
				this.SynchPostProcessDel = synchOptions.SynchPostProcessDel;
				this.SynchPreProcessDel = synchOptions.SynchPreProcessDel;
				this.SynchErrorDel = synchOptions.SynchErrorDel;
			} 
		}
	}

	public class SynchSettings : SynchOptions
	{
		private Synchronize.SynchDel synch;
		public Synchronize.SynchDel Synch
		{
			get { return synch; }
			set { synch = value; }
		}

		public SynchSettings () : base()
		{
		}

		public SynchSettings (SynchOptions synchOptions) : base(synchOptions)
		{
		}
	}
}

