using System.Collections.Generic;
using Mono.Cecil.Cil;

namespace StaticAnalysis.ControlFlow
{
	/// <summary>
	/// Each basic block is a node of the control flow graph for single
	/// method.
	/// 
	/// It has the following desirable properties:
	/// - Single entry point, ie. no instruction within the block is
	///   destination of a branch instruction.
	/// - Single exit point, ie. only the last instuction within the
	///   block can transfer control to different basic block.
	/// - All instructions within the block are executed just once
	///   and in order.
	/// </summary>
	public class BasicBlock
	{
		private readonly int index;
		private readonly Instruction entryPoint;
		private readonly Instruction exitPoint;
		private readonly Instruction[] successors;

		internal BasicBlock(
			int index,
			Instruction entryPoint,
			Instruction exitPoint,
			Instruction[] successors)
		{
			this.index = index;
			this.entryPoint = entryPoint;
			this.exitPoint = exitPoint;
			this.successors = successors;
		}

		/// <summary>
		/// Position of the basic block in the method.
		/// </summary>
		public int Index
		{
			get { return this.index; }
		}

		/// <summary>
		/// First instruction of the basic block.
		/// </summary>
		public Instruction EntryPoint
		{
			get { return this.entryPoint; }
		}

		/// <summary>
		/// Last instruction of the basic block.
		/// </summary>
		public Instruction ExitPoint
		{
			get { return this.exitPoint; }
		}

		/// <summary>
		/// Entry points of basic blocks that may gain control after reaching
		/// the end of this block.
		/// </summary>
		public Instruction[] Successors
		{
			get { return this.successors; }
		}

		/// <summary>
		/// List of all instructions that compose the basic block.
		/// </summary>
		public IEnumerable<Instruction> Instructions
		{
			get
			{
				Instruction afterLastInstruction = this.ExitPoint.Next;
				for (Instruction instruction = this.EntryPoint;
					instruction != afterLastInstruction;
					instruction = instruction.Next)
					yield return instruction;
			}
		}
	}
}
