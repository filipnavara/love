using Mono.Cecil;
using Mono.Cecil.Cil;

namespace StaticAnalysis.ControlFlow
{
	/// <summary>
	/// Represents a single catch clausule in a try-catch statement.
	/// 
	/// For example,
	///     try { ... }
	///     catch (SystemException e) { ... }
	///     catch (ApplicationException e) { ... }
	/// would be represented by two CatchBlock objects.
	/// </summary>
	public class CatchBlock
	{
		private readonly TypeReference catchType;
		private readonly Instruction entryPoint;
		private readonly Instruction exitPoint;

		/// <summary>
		/// Construct a catch block.
		/// </summary>
		/// <param name="catchType">Type of exception that is caught in the block.</param>
		/// <param name="entryPoint">First instruction of the catch block.</param>
		/// <param name="exitPoint">Last instruction of the catch block.</param>
		public CatchBlock(TypeReference catchType, Instruction entryPoint, Instruction exitPoint)
		{
			this.catchType = catchType;
			this.entryPoint = entryPoint;
			this.exitPoint = exitPoint;
		}

		/// <summary>
		/// Type of exception that is caught in the block.
		/// </summary>
		public TypeReference CatchType
		{
			get { return this.catchType; }
		}

		/// <summary>
		/// First instruction of the catch block.
		/// </summary>
		public Instruction EntryPoint
		{
			get { return this.entryPoint; }
		}

		/// <summary>
		/// Last instruction of the catch block.
		/// </summary>
		public Instruction ExitPoint
		{
			get { return this.exitPoint; }
		}
	}
}
