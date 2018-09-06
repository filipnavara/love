using Mono.Cecil;

namespace Love.IntraproceduralAnalysis
{
	class TypeOfHeapObject : UnaliasedHeapObject
	{
		public TypeOfHeapObject(TypeReference type)
			: base(type)
		{
		}

		public override bool Equals(object obj)
		{
			var other = obj as TypeOfHeapObject;
			if (other != null)
				return Type.Equals(other.Type);
			return false;
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		public override string ToString()
		{
			return "typeof " + Type;
		}
	}
}
