using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Love
{
	[Flags]
	public enum AnalysisOptions
	{
		NoAliasing = 1,
		NoAliasingAfterMerge = 2,
		IgnoreSystemNamespace = 4,
		DumbCallGraph = 8,
		DumpLockGraph = 32
	}
}
