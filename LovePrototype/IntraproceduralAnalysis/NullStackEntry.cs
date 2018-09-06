using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil.Cil;
using Mono.Cecil;

namespace Love.IntraproceduralAnalysis
{
	class NullStackEntry : StackEntry
	{
		public static NullStackEntry Null = new NullStackEntry();

		private NullStackEntry()
		{
		}

		public override int GetHashCode()
		{
			return 0xdead;
		}

		public override bool Equals(object obj)
		{
			return obj is NullStackEntry;
		}
	}
}
