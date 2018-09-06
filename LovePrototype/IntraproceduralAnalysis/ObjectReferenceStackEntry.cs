using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil.Cil;
using Mono.Cecil;
using System.Diagnostics.Contracts;

namespace Love.IntraproceduralAnalysis
{
	class ObjectReferenceStackEntry : StackEntry
	{
		public ObjectReferenceStackEntry(HeapObject heapObject)
		{
			Contract.Requires(heapObject != null);
			System.Diagnostics.Debug.Assert(heapObject != null);
			this.Value = heapObject;
		}

		public HeapObject Value { get; private set; }

		public override int GetHashCode()
		{
			return this.Value.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			var other = obj as ObjectReferenceStackEntry;
			if (other == null)
				return false;
			return Equals(this.Value, other.Value);
		}
	}
}
