using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil.Cil;
using Mono.Cecil;
using System.Diagnostics.Contracts;

namespace Love.IntraproceduralAnalysis
{
	class ManagedPointerStackEntry : StackEntry
	{
		private object reference;

		public ManagedPointerStackEntry(VariableReference variableReference)
		{
			Contract.Requires(variableReference != null);
			this.reference = variableReference;
		}

		public ManagedPointerStackEntry(ParameterReference parameterReference)
		{
			Contract.Requires(parameterReference != null);
			this.reference = parameterReference;
		}

		public override int GetHashCode()
		{
			return this.reference.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			var other = obj as ManagedPointerStackEntry;
			if (other == null)
				return false;
			return Equals(this.reference, other.reference);
		}
	}
}
