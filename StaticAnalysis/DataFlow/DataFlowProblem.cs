using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil.Cil;

namespace StaticAnalysis.DataFlow
{
	/// <summary>
	/// An abstract data-flow problem.
	/// </summary>
	/// <typeparam name="TState">State computed by the data-flow algorithm.</typeparam>
	public abstract class DataFlowProblem<TState>
	{
		/// <summary>
		/// Direction of the control-flow graph traversal.
		/// </summary>
		public abstract TraversalDirection Direction { get; }

		/// <summary>
		/// Get initial state for the data-flow analysis.
		/// </summary>
		public abstract TState GetInitialState(ProgramPoint programPoint);
		
		/// <summary>
		/// Apply data-flow transfer function to a state and modify it accordingly.
		/// </summary>
		/// <param name="programPoint">Instruction at the currently computed program point</param>
		/// <param name="state">State before the instruction</param>
		public abstract void ApplyRules(ProgramPoint programPoint, TState state);

		/// <summary>
		/// Merge states on control-flow graph join points.
		/// </summary>
		/// <param name="programPoint">Program point of the join</param>
		/// <param name="states">States from all paths leading to the join point</param>
		/// <returns>Merged state</returns>
		public abstract TState MergeStates(ProgramPoint programPoint, TState[] states);

		/// <summary>
		/// Clone state.
		/// </summary>
		/// <param name="state">Original state</param>
		/// <returns>Cloned state</returns>
		public abstract TState CloneState(TState state);

		/// <summary>
		/// Check if two computed states are equal.
		/// </summary>
		/// <param name="stateA">First state</param>
		/// <param name="stateB">Second state</param>
		/// <returns>true if the states are equal, false otherwise</returns>
		public abstract bool EqualStates(TState stateA, TState stateB);
	}
}
