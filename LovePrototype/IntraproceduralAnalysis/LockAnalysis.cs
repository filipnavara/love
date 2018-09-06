using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using StaticAnalysis;
using StaticAnalysis.DataFlow;

namespace Love.IntraproceduralAnalysis
{
	class LockAnalysis : DataFlowProblem<LockState>
	{
		private readonly AnalysisOptions options;
		private readonly VariableAnalysis variableAnalysis;

		public LockAnalysis(AnalysisOptions options)
		{
			this.options = options;
			this.variableAnalysis = new VariableAnalysis(options);
		}

		public override TraversalDirection Direction
		{
			get { return TraversalDirection.Forward; }
		}

		public override LockState GetInitialState(ProgramPoint programPoint)
		{
			return new LockState(programPoint.Method);
		}

		public override void ApplyRules(ProgramPoint programPoint, LockState state)
		{
			if (programPoint.Instruction.OpCode.Code == Code.Call)
			{
				var reference = (MethodReference)programPoint.Instruction.Operand;
				if (IsMonitorEnter(reference))
				{
					var stackEntry = state.Variables.StackVariables.Skip(reference.Parameters.Count - 1).FirstOrDefault() as ObjectReferenceStackEntry;
					Debug.Assert(stackEntry != null);
					if (stackEntry != null)
					{
						HeapObject lockObject = stackEntry.Value;
						bool acquired;
						state.EnterLock(programPoint, lockObject, out acquired);
					}
				}
				else if (IsMonitorExit(reference))
				{
					var stackEntry = state.Variables.StackVariables.Peek() as ObjectReferenceStackEntry;
					Debug.Assert(stackEntry != null);
					if (stackEntry != null)
					{
						HeapObject lockObject = stackEntry.Value;
						Debug.Assert(state.Locks.Count() > 0);
						state.ExitLock(lockObject);
					}
				}
				else if (IsMonitorWait(reference))
				{
					/*HeapObject lockObject = state.Variables.StackVariables.Skip(reference.Parameters.Count - 1).FirstOrDefault();
					Debug.Assert(lockObject != null);
					if (!state.Locks.Contains(lockObject))
						state.Wait.Add(lockObject);
					if (state.Locks.Count() > 0 && !state.TopLock.Equals(lockObject))
					{
						state.LockGraph.AddVertex(lockObject);
						state.LockGraph.AddEdge(new LockGraphEdge(state.TopLock, lockObject, new ProgramPoint(method, instruction)));
					}*/
				}
			}

			variableAnalysis.ApplyRules(programPoint, state.Variables);
		}

		public override LockState MergeStates(ProgramPoint programPoint, LockState[] states)
		{
			return new LockState(programPoint, states);
		}

		public override LockState CloneState(LockState state)
		{
			return new LockState(state);
		}

		public override bool EqualStates(LockState stateA, LockState stateB)
		{
			return stateA.Equals(stateB);
		}

		private static bool IsMonitorEnter(MethodReference method)
		{
			return
				method.DeclaringType.FullName.Equals("System.Threading.Monitor") &&
				(method.Name.Equals("Enter") || method.Name.Equals("TryEnter") || method.Name.Equals("ReliableEnter"));
		}

		private static bool IsMonitorExit(MethodReference method)
		{
			return
				method.DeclaringType.FullName.Equals("System.Threading.Monitor") &&
				method.Name.Equals("Exit");
		}

		private static bool IsMonitorWait(MethodReference method)
		{
			return
				method.DeclaringType.FullName.Equals("System.Threading.Monitor") &&
				method.Name.Equals("Wait");
		}
	}
}
