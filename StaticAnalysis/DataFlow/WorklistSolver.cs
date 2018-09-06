using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StaticAnalysis.ControlFlow;
using Mono.Cecil;
using QuickGraph.Algorithms.TopologicalSort;
using QuickGraph;
using Mono.Cecil.Cil;
using QuickGraph.Algorithms.Search;

namespace StaticAnalysis.DataFlow
{
	/// <summary>
	/// Traverse the control flow graph using work-list algorithm and
	/// compute data-flow analysis.
	/// </summary>
	/// <typeparam name="TState">State of values of the respective data-flow analysis</typeparam>
	public class WorklistSolver<TState>
	{
		private DataFlowProblem<TState> problem;

		/// <summary>
		/// Construct a solver for particular data-flow problem.
		/// </summary>
		/// <param name="problem">Definition of data-flow problem</param>
		public WorklistSolver(DataFlowProblem<TState> problem)
		{
			this.problem = problem;
		}

		/// <summary>
		/// Traverse the control flow graph using iterative algorithm and
		/// compute forward data-flow analysis.
		/// </summary>
		/// <param name="method">Method to compute the analysis on</param>
		/// <param name="graph">Control-flow graph of the method</param>
		/// <param name="inStates">State values at the input of respective basic blocks</param>
		/// <param name="outStates">State values at the output of respective basic blocks</param>
		/// <remarks>Based on the algorithm given in listing 9.23 int the Purple Dragon Book.</remarks>
		public void Solve(
			MethodDefinition method,
			ControlFlowGraph graph,
			out TState[] inStates,
			out TState[] outStates)
		{
			var blocksToVisit = new Queue<int>();

			// Compute predecessor lookup table
			var predecessors = new Dictionary<int, List<int>>();
			foreach (var basicBlock in graph.BasicBlocks)
				predecessors[basicBlock.Index] = new List<int>();
			foreach (var basicBlock in graph.BasicBlocks)
				foreach (var successorIndex in basicBlock.Successors.Select(i => graph.GetBasicBlockAtInstruction(i).Index))
					predecessors[successorIndex].Add(basicBlock.Index);

			inStates = new TState[graph.BasicBlocks.Length];
			outStates = new TState[graph.BasicBlocks.Length];
			inStates[0] = problem.GetInitialState(new ProgramPoint(method, method.Body.Instructions[0]));

			// Processing the nodes in topological order should reduce the
			// number of recomputations, so enqueue all basic blocks in their
			// topological order.
			//
			// Alternatively the naive way should produce the same results:
			//   blocksToVisit.Enqueue(0); 
			var vertices = new List<BasicBlock>(graph.BasicBlocks.Length);
			var topo = new DepthFirstSearchAlgorithm<BasicBlock, SEquatableEdge<BasicBlock>>(graph.QuickGraph);
			topo.FinishVertex += v => vertices.Insert(0, v);
			topo.Compute();
			//if (problem.Direction == TraversalDirection.Backward)
				//vertices.Reverse();
			foreach (var block in vertices)
				blocksToVisit.Enqueue(block.Index);

			while (blocksToVisit.Count != 0)
			{
				var blockIndex = blocksToVisit.Dequeue();
				var block = graph.BasicBlocks[blockIndex];

				// Merge states computed from predecessors
				var predecessorsForBlock = predecessors[blockIndex];
				var inStatesForBlock = new List<TState>(predecessorsForBlock.Count);
				foreach (var predecessorIndex in predecessorsForBlock)
					if (outStates[predecessorIndex] != null)
						inStatesForBlock.Add(outStates[predecessorIndex]);
				if (inStatesForBlock.Count > 1)
					inStates[blockIndex] = problem.MergeStates(new ProgramPoint(method, block.EntryPoint), inStatesForBlock.ToArray());
				else if (inStatesForBlock.Count == 1)
					inStates[blockIndex] = inStatesForBlock[0];
				else if (inStates[blockIndex] == null)
					continue; // FIXME: Skip the block (probably exception handler)

				// Apply rules to given basic block
				TState outState = problem.CloneState(inStates[blockIndex]);
				foreach (var instruction in block.Instructions)
					problem.ApplyRules(new ProgramPoint(method, instruction), outState);

				// Did the computed OUT state change?
				if (!problem.EqualStates(outState, outStates[blockIndex]))
				{
					outStates[blockIndex] = outState;
					// Queue all successors for recomputation
					foreach (var successorIndex in block.Successors.Select(i => graph.GetBasicBlockAtInstruction(i).Index))
					{
						if (!blocksToVisit.Contains(successorIndex))
							blocksToVisit.Enqueue(successorIndex);
					}
				}
			}
		}

		/// <summary>
		/// Traverse the control flow graph using iterative algorithm and
		/// compute forward data-flow analysis.
		/// </summary>
		/// <param name="method">Method to compute the analysis on</param>
		/// <param name="graph">Control-flow graph of the method</param>
		/// <returns>Summary state for the whole method</returns>
		public TState Solve(
			MethodDefinition method,
			ControlFlowGraph graph)
		{
			TState[] ins, outs;

			// DataFlowTraverse + merge all out state from exit blocks
			Solve(
				method,
				graph,
				out ins,
				out outs);

			var statesToMerge = new List<TState>();
			foreach (var basicBlock in graph.BasicBlocks)
			{
				if (basicBlock.Successors.Length == 0 && outs[basicBlock.Index] != null &&
					basicBlock.ExitPoint.OpCode.Code == Code.Ret)
				{
					statesToMerge.Add(outs[basicBlock.Index]);
				}
			}

			if (statesToMerge.Count == 1)
				return statesToMerge[0];
			var exitPoint = graph.BasicBlocks.Last().ExitPoint;
			if (statesToMerge.Count > 0)
				return problem.MergeStates(new ProgramPoint(method, exitPoint), statesToMerge.ToArray());

			return ins[0];
		}
	}
}
