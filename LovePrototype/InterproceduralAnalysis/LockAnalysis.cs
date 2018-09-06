using System.Collections.Generic;
using System.Linq;
using Love.IntraproceduralAnalysis;
using Mono.Cecil;
using Mono.Cecil.Cil;
using QuickGraph;
using QuickGraph.Algorithms.TopologicalSort;
using StaticAnalysis;
using StaticAnalysis.CallGraph;
using StaticAnalysis.ControlFlow;
using StaticAnalysis.DataFlow;
using QuickGraph.Algorithms.Search;

namespace Love.InterproceduralAnalysis
{
	class LockAnalysis : DataFlowProblem<LockState>
	{
		private readonly CallGraph callGraph;
		private readonly MethodDefinition entryPoint;
		private readonly List<MethodDefinition> threadEntryPoints;
		private readonly Queue<MethodDefinition> methodsToVisitQueue;
		private readonly HashSet<MethodDefinition> methodsToVisitSet;
		private readonly Dictionary<MethodDefinition, LockState> outStates;
		private readonly AnalysisOptions options;
		private readonly IntraproceduralAnalysis.LockAnalysis intraproceduralAnalysis;
		private readonly WorklistSolver<LockState> intraproceduralSolver;

		public LockAnalysis(CallGraph callGraph, MethodDefinition entryPoint, AnalysisOptions options)
		{
			this.intraproceduralAnalysis = new IntraproceduralAnalysis.LockAnalysis(options);
			this.intraproceduralSolver = new WorklistSolver<LockState>(this);
			this.options = options;
			this.callGraph = callGraph;
			/*using (var writer = new System.IO.StreamWriter("calls.txt"))
				foreach (var edge in this.callGraph.QuickGraph.Edges)
					writer.WriteLine(edge.ToString());*/
			this.entryPoint = entryPoint;
			this.methodsToVisitQueue = new Queue<MethodDefinition>();
			this.methodsToVisitSet = new HashSet<MethodDefinition>();
			this.outStates = new Dictionary<MethodDefinition, LockState>();
			this.threadEntryPoints = new List<MethodDefinition>();

			foreach (var method in callGraph.QuickGraph.Vertices)
			{
				if (method.Name.Equals("Invoke"))
				{
					if (method.DeclaringType.FullName.Equals("System.Threading.ThreadStart") ||
						method.DeclaringType.FullName.Equals("System.Threading.ParameterizedThreadStart") ||
						method.DeclaringType.FullName.Equals("System.Threading.WaitCallback") ||
						method.DeclaringType.FullName.Equals("System.Threading.TimerCallback"))
					{
						foreach (var outEdges in callGraph.QuickGraph.OutEdges(method))
							threadEntryPoints.Add(outEdges.Target);
					}
				}
			}

			PopulateMethodsToVisitList();
		}

		private void PopulateMethodsToVisitList()
		{
			var vertices = new List<MethodDefinition>(callGraph.QuickGraph.VertexCount);
			var topo = new DepthFirstSearchAlgorithm<MethodDefinition, CallGraphEdge>(callGraph.QuickGraph);
			topo.FinishVertex += v => vertices.Add(v);
			topo.Compute();
			foreach (var method in vertices)
			{
				this.methodsToVisitQueue.Enqueue(method);
				this.methodsToVisitSet.Add(method);
			}
		}

		public override TraversalDirection Direction
		{
			get { return TraversalDirection.Forward; }
		}

		public override LockState GetInitialState(ProgramPoint programPoint)
		{
			return intraproceduralAnalysis.GetInitialState(programPoint);
		}

		public override void ApplyRules(ProgramPoint programPoint, LockState state)
		{
			if (programPoint.Instruction.OpCode.Code == Code.Callvirt ||
				programPoint.Instruction.OpCode.Code == Code.Call ||
				programPoint.Instruction.OpCode.Code == Code.Newobj)
			{
				foreach (var calledMethod in this.callGraph.GetCallSiteTargets(programPoint))
				{
					if (!calledMethod.Equals(programPoint.Method))
					{
						LockState summary;
						if (this.outStates.TryGetValue(calledMethod, out summary))
						{
							MergeCalleeIntoCaller(
								summary,
								calledMethod,
								state,
								programPoint);
						}
						else
						{
							// Summary for the called method wasn't computed yet, thus the method
							// still has to be on the queue of methods to be computed. Once the
							// called method is recomputed all its callers will be queued for
							// recomputation.
							System.Diagnostics.Debug.Assert(this.methodsToVisitSet.Contains(calledMethod));
						}
					}
				}
			}

			intraproceduralAnalysis.ApplyRules(programPoint, state);
		}

		public override LockState MergeStates(ProgramPoint programPoint, LockState[] states)
		{
			return intraproceduralAnalysis.MergeStates(programPoint, states);
		}

		public override LockState CloneState(LockState state)
		{
			return intraproceduralAnalysis.CloneState(state);
		}

		public override bool EqualStates(LockState stateA, LockState stateB)
		{
			return intraproceduralAnalysis.EqualStates(stateA, stateB);
		}

		private void MergeCalleeIntoCaller(
			LockState callee,
			MethodDefinition calleeMethod,
			LockState caller,
			ProgramPoint callSite)
		{
			RenameCallerToCallee(callee, calleeMethod, caller, callSite);
			// FIXME: Handle waits
		}

		private LockAcquisition RenameCallerToCallee(
			Dictionary<HeapObject, HeapObject> argumentMap,
			LockAcquisition lockAcquisition)
		{
			HeapObject symbolicObject;

			if (!(lockAcquisition.SymbolicObject is ParameterHeapObject) ||
				!argumentMap.TryGetValue(lockAcquisition.SymbolicObject, out symbolicObject) ||
				symbolicObject == null)
			{
				if (lockAcquisition.SymbolicObject is UnaliasedHeapObject ||
					lockAcquisition.SymbolicObject.ProgramPoint == null)
					return lockAcquisition;
				if ((this.options & AnalysisOptions.NoAliasingAfterMerge) != 0)
					symbolicObject = new UnaliasedHeapObject(lockAcquisition.SymbolicObject.Type);
				else
					symbolicObject = new HeapObject(null, lockAcquisition.SymbolicObject.Type);
			}

			return new LockAcquisition(lockAcquisition.ProgramPoint, symbolicObject);
		}

		private void RenameCallerToCallee(
			LockState callee,
			MethodDefinition calleeMethod,
			LockState caller,
			ProgramPoint callSite)
		{
			if (callee.Roots.Count > 0)
			{
				var actualArguments = caller.Variables.StackVariables.Take(callee.Variables.ParameterVariables.Length).Reverse();
				var formalArguments = callee.Variables.ParameterVariables;
				var argumentMap = new Dictionary<HeapObject, HeapObject>();
				int argumentIndex = 0;
				foreach (var actualArgument in actualArguments)
					argumentMap[formalArguments[argumentIndex++]] = (actualArgument is ObjectReferenceStackEntry) ? ((ObjectReferenceStackEntry)actualArgument).Value : null;

				ISet<LockAcquisition> renamedRoots;
				BidirectionalGraph<LockAcquisition, LockGraphEdge> renamedLockGraph;

				if (caller.Locks.Count > 0)
				{
					renamedRoots = new HashSet<LockAcquisition>();
					renamedLockGraph = new BidirectionalGraph<LockAcquisition, LockGraphEdge>();
				}
				else
				{
					renamedRoots = caller.Roots;
					renamedLockGraph = caller.LockGraph;
				}
				
				foreach (var root in callee.Roots)
					renamedRoots.Add(RenameCallerToCallee(argumentMap, root));
				foreach (var lockEdge in callee.LockGraph.Edges)
				{
					var renamedSource = RenameCallerToCallee(argumentMap, lockEdge.Source);
					var renamedTarget = RenameCallerToCallee(argumentMap, lockEdge.Target);
					//if (!renamedSource.Equals(renamedTarget))
					{
						LockGraphEdge newEdge;
						if (lockEdge.Source == renamedSource && lockEdge.Target == renamedTarget)
							newEdge = lockEdge;
						else
							newEdge = new LockGraphEdge(renamedSource, lockEdge.SourceProgramPoint, renamedTarget, lockEdge.TargetProgramPoint);
						renamedLockGraph.AddVerticesAndEdge(newEdge);
					}
				}

				if (caller.Locks.Count > 0)
				{
					foreach (var heldLock in caller.Locks)
					{
						if (renamedRoots.Contains(heldLock))
						{
							renamedRoots.Remove(heldLock);
							if (renamedLockGraph.ContainsVertex(heldLock))
							{
								foreach (var outEdge in renamedLockGraph.OutEdges(heldLock))
									renamedRoots.Add(outEdge.Target);
							}
						}

						if (renamedLockGraph.ContainsVertex(heldLock))
						{
							var edgesToAdd = new List<LockGraphEdge>();
							foreach (var inEdge in renamedLockGraph.InEdges(heldLock))
								foreach (var outEdge in renamedLockGraph.OutEdges(heldLock))
									if (!Equals(inEdge.Source, outEdge.Target))
										edgesToAdd.Add(new LockGraphEdge(inEdge.Source, inEdge.SourceProgramPoint, outEdge.Target, outEdge.TargetProgramPoint));
							renamedLockGraph.RemoveVertex(heldLock);
							renamedLockGraph.AddEdgeRange(edgesToAdd);
						}
					}

					caller.LockGraph.AddVertexRange(renamedRoots);
					caller.LockGraph.AddVertexRange(renamedLockGraph.Vertices);
					caller.LockGraph.AddEdgeRange(renamedLockGraph.Edges);

					var topLock = caller.TopLock;
					foreach (var renamedRoot in renamedRoots)
						caller.LockGraph.AddEdge(new LockGraphEdge(
							topLock,
							callSite,
							renamedRoot,
							renamedRoot.ProgramPoint));
				}
				else
				{
					caller.LockGraph.AddVertexRange(renamedRoots);
				}
			}
		}

		public LockState ComputeMethodSummary(MethodDefinition method)
		{
			LockState outState;
			bool analyzeMethodBody;

			System.Diagnostics.Debug.Assert(!this.outStates.ContainsKey(method));

			analyzeMethodBody = method.HasBody;
			if (method.DeclaringType.FullName.Equals("System.Threading.Monitor"))
				analyzeMethodBody = false;
			else if ((this.options & AnalysisOptions.IgnoreSystemNamespace) != 0 && method.DeclaringType.FullName.StartsWith("System"))
				analyzeMethodBody = false;
			
			if (analyzeMethodBody)
			{
				var controlFlowGraph = new ControlFlowGraph(method.Body);
				outState = this.intraproceduralSolver.Solve(method, controlFlowGraph);
				outState.Compact();
				method.Body = null;
			}
			else
			{
				outState = new LockState(method);
				foreach (var calledMethodEdge in this.callGraph.QuickGraph.OutEdges(method))
				{
					var calledMethod = calledMethodEdge.Target;
					if (!calledMethod.Equals(method))
					{
						LockState summary;
						if (this.outStates.TryGetValue(calledMethod, out summary))
						{
							MergeCalleeIntoCaller(
								summary,
								calledMethod,
								outState,
								new ProgramPoint(method, null));
						}
						else
						{
							// Summary for the called method wasn't computed yet, thus the method
							// still has to be on the queue of methods to be computed. Once the
							// called method is recomputed all its callers will be queued for
							// recomputation.
							System.Diagnostics.Debug.Assert(this.methodsToVisitSet.Contains(calledMethod));
						}
					}
				}
			}

			System.Diagnostics.Debug.Assert(outState.Locks.Count == 0);

			this.outStates[method] = outState;
			return outState;
		}

		public LockState Compute()
		{
			while (methodsToVisitQueue.Count != 0)
			{
				var method = methodsToVisitQueue.Dequeue();
				LockState outState, oldOutState;

				methodsToVisitSet.Remove(method);
				outState = ComputeMethodSummary(method);

				// Did the computed OUT state change?
				outStates.TryGetValue(method, out oldOutState);
				if (!outState.Equals(oldOutState))
				{
					outStates[method] = outState;
					// Queue all predecessors for recomputation
					IEnumerable<CallGraphEdge> inEdges;
					if (this.callGraph.QuickGraph.TryGetInEdges(method, out inEdges))
					{
						var predecessors = inEdges.Select(edge => edge.Source).Distinct();
						foreach (var predecessor in predecessors)
						{
							if (!methodsToVisitSet.Contains(predecessor))
							{
								methodsToVisitQueue.Enqueue(predecessor);
								methodsToVisitSet.Add(predecessor);
							}
						}
					}
				}
			}

			var mainThreadLockState = outStates[entryPoint];
			var mergedlockState = new LockState(entryPoint);

			foreach (var rootEntryPoint in 
				new[] { entryPoint }.Union(
				threadEntryPoints.Union(
				callGraph.QuickGraph.Vertices.Where(m => m.IsConstructor && m.IsStatic))))
			{
				MergeCalleeIntoCaller(outStates[rootEntryPoint], rootEntryPoint, mergedlockState, null);
				outStates.Remove(rootEntryPoint); // Save memory
			}

			return mergedlockState;
		}
	}
}
