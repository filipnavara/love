using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using QuickGraph;
using StaticAnalysis;

namespace Love.IntraproceduralAnalysis
{
	class LockState : IEquatable<LockState>
	{
		private readonly VariableState variables;
		private readonly BidirectionalGraph<LockAcquisition, LockGraphEdge> lockGraph;
		private readonly HashSet<LockAcquisition> roots;
		private readonly Stack<LockAcquisition> locks;
		private readonly HashSet<HeapObject> wait;
		
		public LockState(MethodDefinition method)
		{
			this.lockGraph = new BidirectionalGraph<LockAcquisition, LockGraphEdge>(false);
			this.roots = new HashSet<LockAcquisition>();
			this.locks = new Stack<LockAcquisition>();
			this.wait = new HashSet<HeapObject>();
			this.variables = new VariableState(method);
		}

		public LockState(LockState state)
		{
			this.lockGraph = new BidirectionalGraph<LockAcquisition, LockGraphEdge>(false);
			this.lockGraph.AddVertexRange(state.LockGraph.Vertices);
			this.lockGraph.AddEdgeRange(state.LockGraph.Edges);
			this.roots = new HashSet<LockAcquisition>(state.Roots);
			this.locks = new Stack<LockAcquisition>(state.Locks.Reverse());
			this.wait = new HashSet<HeapObject>(state.Wait);
			this.variables = new VariableState(state.Variables);
		}

		public LockState(ProgramPoint joinPoint, LockState[] states)
		{
			this.lockGraph = new BidirectionalGraph<LockAcquisition, LockGraphEdge>(false);
			this.roots = new HashSet<LockAcquisition>();
			this.wait = new HashSet<HeapObject>();
			this.locks = new Stack<LockAcquisition>(states[states.Length - 1].locks.Reverse());

			foreach (var state in states)
			{
				this.lockGraph.AddVertexRange(state.LockGraph.Vertices);
				this.lockGraph.AddEdgeRange(state.LockGraph.Edges);
				this.roots.UnionWith(state.Roots);
				this.wait.UnionWith(state.Wait);
				this.locks = CommonLockSequence(this.locks, state.locks);
				Debug.Assert(state.Variables.StackVariables.Count == states[0].Variables.StackVariables.Count);
			}

			Debug.Assert(this.LockGraph.VertexCount >= this.locks.Count);

			this.variables = new VariableState(joinPoint, states.Select(s => s.variables).ToArray());
		}

		private static Stack<LockAcquisition> CommonLockSequence(
			Stack<LockAcquisition> a,
			Stack<LockAcquisition> b)
		{
			var aEnumerator = a.Reverse().GetEnumerator();
			var bEnumerator = b.Reverse().GetEnumerator();
			var result = new Stack<LockAcquisition>();

			while (aEnumerator.MoveNext() && bEnumerator.MoveNext() &&
				Equals(aEnumerator.Current, bEnumerator.Current))
			{
				result.Push(aEnumerator.Current);
			}

			return result;
		}

		public BidirectionalGraph<LockAcquisition, LockGraphEdge> LockGraph { get { return this.lockGraph; } }
		public ISet<LockAcquisition> Roots { get { return this.roots; } }
		public Stack<LockAcquisition> Locks { get { return this.locks; } }
		public ISet<HeapObject> Wait { get { return this.wait; } }
		public VariableState Variables { get { return this.variables; } }

		public LockAcquisition TopLock
		{
			get
			{
				var tempSet = new HashSet<LockAcquisition>();
				LockAcquisition topLock = null;
				foreach (var lockObject in this.locks.Reverse())
				{
					if (!tempSet.Contains(lockObject))
					{
						topLock = lockObject;
						tempSet.Add(lockObject);
					}
				}
				return topLock;
			}
		}

		public void EnterLock(ProgramPoint programPoint, HeapObject symbolicObject, out bool entered)
		{
			var lockAcquisition = new LockAcquisition(programPoint, symbolicObject);
			if (this.Locks.FirstOrDefault(la => lockAcquisition.SymbolicObject.Equals(la.SymbolicObject)) == null)
			{
				this.lockGraph.AddVertex(lockAcquisition);
				if (this.locks.Count == 0)
				{
					this.roots.Add(lockAcquisition);
				}
				else
				{
					this.lockGraph.AddEdge(new LockGraphEdge(
						this.locks.Peek(),
						this.locks.Peek().ProgramPoint,
						lockAcquisition,
						programPoint));
				}
				entered = true;
			}
			else
			{
				entered = false;
			}
			this.locks.Push(lockAcquisition);
		}

		public void ExitLock(HeapObject symbolicObject)
		{
			if (this.locks.Count() > 0)
			{
				var lastLock = this.locks.Peek().SymbolicObject;
				if (lastLock.Equals(symbolicObject))
				{
					this.locks.Pop();
				}
				else
				{
					// FIXME: Assume that unlock on the same field is aliased
					if (lastLock.ProgramPoint != null &&
						IsLoadOfField(lastLock.ProgramPoint.Instruction.OpCode) &&
						symbolicObject.ProgramPoint != null &&
						IsLoadOfField(symbolicObject.ProgramPoint.Instruction.OpCode) &&
						Equals(lastLock.ProgramPoint.Instruction.Operand, symbolicObject.ProgramPoint.Instruction.Operand))
					{
						this.locks.Pop();
					}
					else
					{
						Debug.Assert(false);
					}
				}
			}
		}

		public void Compact()
		{
			Array.Clear(this.variables.LocalVariables, 0, this.variables.LocalVariables.Length);
			this.variables.StackVariables.Clear();
		}

		private static bool IsLoadOfField(OpCode opcode)
		{
			return opcode.Code == Code.Ldfld || opcode.Code == Code.Ldsfld;
		}

		public bool Equals(LockState other)
		{
			if (other == null)
				return false;
			if (!this.variables.Equals(other.variables))
				return false;
			if (other.locks.Count != this.locks.Count)
				return false;
			/*if (!other.Locks.SequenceEqual(this.Locks))
				return false;*/
			if (!other.Roots.SetEquals(this.Roots))
				return false;
			if (!other.Wait.SetEquals(this.Wait))
				return false;
			// FIXME: Compare  LockGraph*/
			return true;
		}
	}
}
