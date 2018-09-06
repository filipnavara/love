using StaticAnalysis;

namespace Love.IntraproceduralAnalysis
{
	public class LockAcquisition
	{
		public LockAcquisition(ProgramPoint programPoint, HeapObject symbolicObject)
		{
			this.ProgramPoint = programPoint;
			this.SymbolicObject = symbolicObject;
		}

		public ProgramPoint ProgramPoint { get; private set; }
		public HeapObject SymbolicObject { get; private set; }

		public override bool Equals(object obj)
		{
			var other = obj as LockAcquisition;
			if (other == null)
				return false;
			if (this.SymbolicObject is UnaliasedHeapObject != other.SymbolicObject is UnaliasedHeapObject)
				return false;
			return this.SymbolicObject.Equals(other.SymbolicObject);
		}

		public override int GetHashCode()
		{
			return unchecked(this.SymbolicObject.GetHashCode());
		}

		public override string ToString()
		{
			return this.SymbolicObject.ToString();
		}
	}
}
