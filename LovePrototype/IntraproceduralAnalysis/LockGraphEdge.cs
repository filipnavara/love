using QuickGraph;
using Mono.Cecil;
using StaticAnalysis;

namespace Love.IntraproceduralAnalysis
{
	public class LockGraphEdge : EquatableEdge<LockAcquisition>
	{
		public LockGraphEdge(
			LockAcquisition source,
			ProgramPoint sourceProgramPoint,
			LockAcquisition target,
			ProgramPoint targetProgramPoint)
			: base(source, target)
		{
			System.Diagnostics.Debug.Assert(!source.Equals(target));
			this.SourceProgramPoint = sourceProgramPoint;
			this.TargetProgramPoint = targetProgramPoint;
		}

		public ProgramPoint SourceProgramPoint { get; private set; }
		public ProgramPoint TargetProgramPoint { get; private set; }

		public override bool Equals(object obj)
		{
			var other = obj as LockGraphEdge;
			if (other != null)
			{
				if (!Equals(other.SourceProgramPoint, this.SourceProgramPoint))
					return false;
				if (!Equals(other.TargetProgramPoint, this.TargetProgramPoint))
					return false;
			}
			return base.Equals(obj);
		}
	}
}
