using System.Collections.Generic;
using Mono.Cecil;
using QuickGraph;
using System.Diagnostics.Contracts;

namespace StaticAnalysis.CallGraph
{
	/// <summary>
	/// Static call graph abstraction with helper methods.
	/// 
	/// The call graph can be constructed by using a call graph builder, such as
	/// <see cref="ChaCallGraphBuilder"/>.
	/// </summary>
	public class CallGraph
	{
		private readonly IBidirectionalGraph<MethodDefinition, CallGraphEdge> callGraph;

		/// <summary>
		/// Construct call graph reachable from a given root method.
		/// </summary>
		/// <param name="callGraph">Representation of the call graph</param>
		public CallGraph(IBidirectionalGraph<MethodDefinition, CallGraphEdge> callGraph)
		{
			Contract.Requires(callGraph != null);
			this.callGraph = callGraph;
		}

		/// <summary>
		/// Get all possible methods that could be called at specific callsite.
		/// </summary>
		/// <param name="programPoint">Program point of the callsite</param>
		/// <returns>All possible methods called at given callsite</returns>
		public IEnumerable<MethodDefinition> GetCallSiteTargets(ProgramPoint programPoint)
		{
			Contract.Requires(programPoint != null);
			IEnumerable<CallGraphEdge> outEdges;
			if (this.QuickGraph.TryGetOutEdges(programPoint.Method, out outEdges))
			{
				foreach (var edge in outEdges)
					if (Equals(edge.ProgramPoint, programPoint))
						yield return edge.Target;
			}			
		}

		/// <summary>
		/// Get a representation of the call graph as QuickGraph object that
		/// can be used for special algorithmic analyses or for vizualization
		/// of the graph.
		/// </summary>
		public IBidirectionalGraph<MethodDefinition, CallGraphEdge> QuickGraph
		{
			get { return this.callGraph; }
		}
	}
}
