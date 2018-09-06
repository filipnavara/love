using Mono.Cecil.Cil;
using Mono.Cecil;
using System.Diagnostics.Contracts;
using System;

namespace StaticAnalysis
{
	/// <summary>
	/// Defines a unique representation of point in program code using a tuple
	/// of method and its instruction.
	/// </summary>
	public class ProgramPoint
	{
		private readonly MethodDefinition method;
		private readonly WeakReference instruction;
		private readonly int offset;

		/// <summary>
		/// Create a program point.
		/// </summary>
		/// <param name="method">Method containing the instruction</param>
		/// <param name="instruction">Instruction of the method</param>
		public ProgramPoint(MethodDefinition method, Instruction instruction)
		{
			Contract.Requires(method != null);
			this.method = method;
			this.instruction = new WeakReference(instruction);
			this.offset = instruction == null ? 0 : instruction.Offset;
		}

		/// <summary>
		/// Returns a readable representation of the program point including
		/// file and line number if debugging symbols are available and loaded.
		/// </summary>
		/// <returns>Readable representation of program point</returns>
		public override string ToString()
		{
			return "method " + Method + "+0x" + offset.ToString("x");
		}

		/// <summary>
		/// Serves as a hash function for a particular type.
		/// </summary>
		/// <returns>A hash code for the current ProgramPoint.</returns>
		public override int GetHashCode()
		{
			return unchecked(this.method.GetHashCode() + offset);
		}

		/// <summary>
		/// Determines whether the specified Object is equal to the current Object.
		/// </summary>
		/// <param name="obj">The object to compare with the current object.</param>
		/// <returns>true if the specified Object is equal to the current Object; otherwise, false.</returns>
		public override bool Equals(object obj)
		{
			var other = obj as ProgramPoint;
			if (other == null)
				return false;
			return this.method.Equals(other.method) && offset == other.offset;
		}

		/// <summary>
		/// Method of the program point containing the instruction.
		/// </summary>
		public MethodDefinition Method
		{
			get { return this.method; }
		}

		/// <summary>
		/// Offset of instruction of the program point from the specified method.
		/// </summary>
		public int Offset
		{
			get { return this.offset; }
		}

		/// <summary>
		/// Instruction of the program point from the specified method.
		/// </summary>
		public Instruction Instruction
		{
			get
			{
				Instruction i = this.instruction.Target as Instruction;
				if (i != null)
					return i;

				var methodDefinition = Method.Resolve();
				if (methodDefinition != null && methodDefinition.HasBody)
				{
					foreach (var instruction in methodDefinition.Body.Instructions)
						if (instruction.Offset == this.offset)
							return instruction;
				}

				return null;
			}
		}
	}
}
