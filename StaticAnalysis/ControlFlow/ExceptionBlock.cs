using Mono.Cecil.Cil;

namespace StaticAnalysis.ControlFlow
{
	/// <summary>
	/// Represents an try/catch/finally exception block in a function.
	/// </summary>
	public class ExceptionBlock
	{
		private readonly Instruction tryEntryPoint;
		private readonly Instruction tryExitPoint;
		private readonly CatchBlock[] catchBlocks;
		private readonly Instruction finallyEntryPoint;
		private readonly Instruction finallyExitPoint;
		private readonly Instruction faultEntryPoint;
		private readonly Instruction faultExitPoint;

		/// <summary>
		/// Construct an exception block information.
		/// </summary>
		/// <param name="tryEntryPoint">First instruction of the try block.</param>
		/// <param name="tryExitPoint">Last instruction of the try block.</param>
		/// <param name="catchBlocks">Array of all catch blocks belonging the the try region.</param>
		/// <param name="finallyEntryPoint">First instruction of the finally block, or null if no finally block is present.</param>
		/// <param name="finallyExitPoint">Last instruction of the finally block, or null if no finally block is present.</param>
		/// <param name="faultEntryPoint">First instruction of the fault block, or null if no fault block is present.</param>
		/// <param name="faultExitPoint">Last instruction of the fault block, or null if no fault block is present.</param>
		public ExceptionBlock(
			Instruction tryEntryPoint,
			Instruction tryExitPoint,
			CatchBlock[] catchBlocks,
			Instruction finallyEntryPoint,
			Instruction finallyExitPoint,
			Instruction faultEntryPoint,
			Instruction faultExitPoint)
		{
			this.tryEntryPoint = tryEntryPoint;
			this.tryExitPoint = tryExitPoint;
			this.catchBlocks = catchBlocks;
			this.finallyEntryPoint = finallyEntryPoint;
			this.finallyExitPoint = finallyExitPoint;
			this.faultEntryPoint = faultEntryPoint;
			this.faultExitPoint = faultExitPoint;
		}

		/// <summary>
		/// First instruction of the try block.
		/// </summary>
		public Instruction TryEntryPoint
		{
			get { return this.tryEntryPoint; }
		}

		/// <summary>
		/// Last instruction of the try block.
		/// </summary>
		public Instruction TryExitPoint
		{
			get { return this.tryExitPoint; }
		}

		/// <summary>
		/// Array of all catch blocks belonging the the try region.
		/// </summary>
		public CatchBlock[] CatchBlocks
		{
			get { return this.catchBlocks; }
		}

		/// <summary>
		/// First instruction of the finally block, or null if no finally block is present.
		/// </summary>
		public Instruction FinallyEntryPoint
		{
			get { return this.finallyEntryPoint; }
		}

		/// <summary>
		/// Last instruction of the finally block, or null if no finally block is present.
		/// </summary>
		public Instruction FinallyExitPoint
		{
			get { return this.finallyExitPoint; }
		}

		/// <summary>
		/// First instruction of the fault block, or null if no fault block is present.
		/// </summary>
		public Instruction FaultEntryPoint
		{
			get { return this.faultEntryPoint; }
		}

		/// <summary>
		/// Last instruction of the fault block, or null if no fault block is present.
		/// </summary>
		public Instruction FaultExitPoint
		{
			get { return this.faultExitPoint; }
		}
	}
}
