using System;

namespace SQLiteXM
{
	public class SynchOptions
	{
		private SynchErrorDel synchErrorDel;
		public SynchErrorDel SynchErrorDel
		{
			get { return synchErrorDel; }
			set { synchErrorDel = value; }
		}

		private SynchPreProcessDel synchPreProcessDel;
		public SynchPreProcessDel SynchPreProcessDel
		{
			get { return synchPreProcessDel; }
			set { synchPreProcessDel = value; }
		}

		private SynchPostProcessDel synchPostProcessDel;
		public SynchPostProcessDel SynchPostProcessDel
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
		private SynchDel synchDel;
		public SynchDel SynchDel
		{
			get { return synchDel; }
			set { synchDel = value; }
		}

		public SynchSettings () : base()
		{
		}

		public SynchSettings (SynchOptions synchOptions) : base(synchOptions)
		{
		}
	}
}

