using System;
using System.Collections.Generic;
using Love.IntraproceduralAnalysis;
using Mono.Cecil;
using QuickGraph;
using QuickGraph.Algorithms;
using StaticAnalysis.CallGraph;

namespace Love
{
	class LockGraph
	{
		private readonly BidirectionalGraph<LockAcquisition, LockGraphEdge> lockGraph;
		private readonly ISet<LockAcquisition> roots;
		private readonly AnalysisOptions options;

		public LockGraph(CallGraph callGraph, MethodDefinition entryPoint, AnalysisOptions options)
		{
			var interproceduralAnalysis = new InterproceduralAnalysis.LockAnalysis(
				callGraph, entryPoint, options);
			var lockState = interproceduralAnalysis.Compute();
			this.lockGraph = lockState.LockGraph;
			this.roots = lockState.Roots;
			this.options = options;
		}

		public IBidirectionalGraph<LockAcquisition, LockGraphEdge> Graph
		{
			get { return this.lockGraph; }
		}

		public ISet<LockAcquisition> Roots
		{
			get { return this.roots; }
		}
	}
}
