using Mono.Cecil;

namespace Love.IntraproceduralAnalysis
{
	class UnaliasedFieldHeapObject : UnaliasedHeapObject
	{
		readonly FieldDefinition field;

		public UnaliasedFieldHeapObject(FieldDefinition field)
			: base(field.FieldType)
		{
			this.field = field;
		}

		public override bool Equals(object obj)
		{
			var other = obj as UnaliasedFieldHeapObject;
			if (other != null)
				return field.Equals(other.field);
			return false;
		}

		public override string ToString()
		{
			return "field " + field;
		}
	}
}
