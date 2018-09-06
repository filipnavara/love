using System;
using System.Collections.Generic;
using System.Threading;
using Mono.Cecil;
using Mono.Cecil.Cil;
using QuickGraph;
using StaticAnalysis.ClassHierarchy;
using System.Diagnostics.Contracts;

namespace StaticAnalysis.CallGraph
{
	/// <summary>
	/// Call graph implementation that completely ignores virtual method
	/// resolution and delegates.
	/// </summary>
	public class DumbCallGraphBuilder : CallGraphBuilder
	{
		private MethodDefinition rootMethod;

		/// <summary>
		/// Construct a call graph reachable from a given root method.
		/// </summary>
		/// <param name="rootMethod">Root method (eg. assembly entrypoint or thread entrypoint)</param>
		public DumbCallGraphBuilder(MethodDefinition rootMethod)
		{
			Contract.Requires(rootMethod != null);
			this.rootMethod = rootMethod;
		}

		/// <summary>
		/// Build the call graph.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token that allows to interrupt the call graph construction.</param>
		public override CallGraph Build(CancellationToken cancellationToken)
		{
			var classHiearchyGraph = new ClassHierarchyGraph(rootMethod.DeclaringType);
			var callGraph = new BidirectionalGraph<MethodDefinition, CallGraphEdge>(false);
			AnalyzeCalls(callGraph, classHiearchyGraph, rootMethod, cancellationToken);
			return new CallGraph(callGraph);
		}

		private static void AnalyzeCalls(
			BidirectionalGraph<MethodDefinition, CallGraphEdge> callGraph,
			ClassHierarchyGraph classHiearchyGraph,
			MethodDefinition rootMethod,
			CancellationToken cancellationToken)
		{
			var methodsToAnalyze = new Stack<MethodDefinition>();
			var calledMethods = new List<Tuple<MethodDefinition, Instruction>>();

			// Start analysis from the root method
			methodsToAnalyze.Push(rootMethod);
			callGraph.AddVertex(rootMethod);

			while (methodsToAnalyze.Count > 0)
			{
				MethodDefinition method = methodsToAnalyze.Pop();

				cancellationToken.ThrowIfCancellationRequested();

				// For each method with body add edges for any called method. If the target
				// method wasn't analyzed yet then add it to queue. Also enqueue all
				// referenced methods (ldftn) since they may later be connected during the
				// delegate invocation analysis.
				if (method.HasBody)
				{
					foreach (var instruction in method.Body.Instructions)
					{
						if (instruction.OpCode.FlowControl == FlowControl.Call &&
							instruction.OpCode.Code != Code.Calli)
						{
							var callTarget = ((MethodReference)instruction.Operand).Resolve();
							if (callTarget != null)
								calledMethods.Add(Tuple.Create(callTarget, instruction));
						}
					}

					foreach (var target in calledMethods)
					{
						if (!callGraph.ContainsVertex(target.Item1))
						{
							methodsToAnalyze.Push(target.Item1);
							callGraph.AddVertex(target.Item1);
						}
						callGraph.AddEdge(new CallGraphEdge(new ProgramPoint(method, target.Item2), target.Item1));
					}
					calledMethods.Clear();
				}
			}
		}
	}
}
