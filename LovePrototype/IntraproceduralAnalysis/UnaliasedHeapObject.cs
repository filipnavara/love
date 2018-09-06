using Mono.Cecil;

namespace Love.IntraproceduralAnalysis
{
	class UnaliasedHeapObject : HeapObject
	{
		public UnaliasedHeapObject(TypeReference type)
			: base(null, type)
		{
		}

		public override bool Equals(object obj)
		{
			return ReferenceEquals(obj, this);
		}
	}
}
