using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using StaticAnalysis.ClassHierarchy;
using StaticAnalysis;

namespace Love.IntraproceduralAnalysis
{
	class VariableState : IEquatable<VariableState>
	{
		private readonly HeapObject[] parameterVariables;
		private readonly HeapObject[] localVariables;
		private readonly Stack<StackEntry> stackVariables;

		public VariableState(MethodDefinition method)
		{
			Instruction firstInstruction;

			if (method.HasBody)
			{
				this.localVariables = new HeapObject[method.Body.Variables.Count];
				this.stackVariables = new Stack<StackEntry>(method.Body.MaxStackSize);
				firstInstruction = method.Body.Instructions[0];
			}
			else
			{
				this.localVariables = new HeapObject[0];
				this.stackVariables = new Stack<StackEntry>();
				firstInstruction = null;
			}
			this.parameterVariables = new HeapObject[method.Parameters.Count + (method.HasThis ? 1 : 0)];
			int parameterIndex = 0;
			if (method.HasThis)
				this.parameterVariables[parameterIndex++] = new HeapObject(new ProgramPoint(method, firstInstruction), method.DeclaringType);
			foreach (var parameter in method.Parameters)
				this.parameterVariables[parameterIndex++] = new ParameterHeapObject(parameter);
		}

		public VariableState(VariableState state)
		{
			this.parameterVariables = state.ParameterVariables.ToArray();
			this.localVariables = state.LocalVariables.ToArray();
			this.stackVariables = new Stack<StackEntry>(state.StackVariables.Reverse());
		}

		public VariableState(ProgramPoint joinPoint, VariableState[] states)
		{
			if (states.Length == 1)
			{
				this.parameterVariables = states[0].ParameterVariables.ToArray();
				this.localVariables = states[0].LocalVariables.ToArray();
				this.stackVariables = new Stack<StackEntry>(states[0].StackVariables.Reverse());
			}
			else
			{
				this.parameterVariables = MergeVariables(states[0].ParameterVariables, states[1].ParameterVariables, joinPoint);
				this.localVariables = MergeVariables(states[0].LocalVariables, states[1].LocalVariables, joinPoint);
				this.stackVariables = MergeStacks(states[0].StackVariables, states[1].StackVariables, joinPoint);
				for (int state = 2; state < states.Length; state++)
				{
					this.parameterVariables = MergeVariables(this.ParameterVariables, states[state].ParameterVariables, joinPoint);
					this.localVariables = MergeVariables(this.LocalVariables, states[state].LocalVariables, joinPoint);
					this.stackVariables = MergeStacks(this.StackVariables, states[state].StackVariables, joinPoint);
				}
			}
		}

		private static HeapObject MergeHeapObjects(HeapObject a, HeapObject b, ProgramPoint joinPoint)
		{
			if (a == null)
				return b;
			else if (b == null)
				return a;
			var leastCommonType = ClassHierarchyGraph.GetLeastCommonAncestor(a.Type.Resolve(), b.Type.Resolve());
			if (leastCommonType == null)
				leastCommonType = joinPoint.Method.Module.TypeSystem.Object.Resolve();
			// FIXME: Arrays are handled incorrectly
			if (a.Type is ArrayType && b.Type is ArrayType)
				return new HeapObject(joinPoint, new ArrayType(leastCommonType));
			return new HeapObject(joinPoint, leastCommonType);
		}

		private static Stack<StackEntry> MergeStacks(Stack<StackEntry> stackA, Stack<StackEntry> stackB, ProgramPoint joinPoint)
		{
			List<StackEntry> newStack = new List<StackEntry>(stackA.Count);
			foreach (var stackEntry in stackA.Zip(stackB, (a, b) => Tuple.Create(a, b)))
			{
				// FIXME: Equals
				if (Equals(stackEntry.Item1, stackEntry.Item2))
				{
					newStack.Insert(0, stackEntry.Item1);
				}
				else if (stackEntry.Item1 is ObjectReferenceStackEntry && stackEntry.Item2 is ObjectReferenceStackEntry)
				{
					newStack.Insert(0, new ObjectReferenceStackEntry(MergeHeapObjects(
						((ObjectReferenceStackEntry)stackEntry.Item1).Value,
						((ObjectReferenceStackEntry)stackEntry.Item2).Value,
						joinPoint)));
				}
				else
				{
					newStack.Insert(0, NullStackEntry.Null);
				}
			}

			return new Stack<StackEntry>(newStack);
		}

		private static HeapObject[] MergeVariables(HeapObject[] variablesA, HeapObject[] variablesB, ProgramPoint joinPoint)
		{
			var newVariables = new HeapObject[variablesA.Length];
			for (var local = 0; local < variablesA.Length; local++)
			{
				if (Equals(variablesA[local], variablesB[local]))
					newVariables[local] = variablesA[local];
				else
					newVariables[local] = MergeHeapObjects(variablesA[local], variablesB[local], joinPoint);
			}
			return newVariables;
		}

		public HeapObject[] ParameterVariables { get { return this.parameterVariables; } }
		public HeapObject[] LocalVariables { get { return this.localVariables; } }
		public Stack<StackEntry> StackVariables { get { return this.stackVariables; } }

		public bool Equals(VariableState other)
		{
			if (other == null)
				return false;
			//if (!other.LocalVariables.SequenceEqual(this.LocalVariables))
				//return false;
			if (!other.ParameterVariables.SequenceEqual(this.ParameterVariables))
				return false;
			if (!other.StackVariables.SequenceEqual(this.StackVariables))
				return false;
			return true;
		}
	}
}
