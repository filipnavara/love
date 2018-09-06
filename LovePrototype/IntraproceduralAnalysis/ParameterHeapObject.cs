using Mono.Cecil;

namespace Love.IntraproceduralAnalysis
{
	class ParameterHeapObject : HeapObject
	{
		readonly ParameterDefinition parameter;

		public ParameterHeapObject(ParameterDefinition parameter)
			: base(null, parameter.ParameterType)
		{
			this.parameter = parameter;
		}

		public override bool Equals(object obj)
		{
			var other = obj as ParameterHeapObject;
			if (other == null)
				return false;
			return Equals(parameter, other.parameter);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		public override string ToString()
		{
			return "parameter " + parameter;
		}
	}
}
