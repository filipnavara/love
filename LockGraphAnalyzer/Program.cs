using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using QuickGraph;
using QuickGraph.Algorithms;
using QuickGraph.Algorithms.TopologicalSort;
using QuickGraph.Algorithms.Search;
using GmlParser;

namespace LockGraphAnalyzer
{
	class Program
	{
		static string ShortenMethodLabel(string label)
		{
			int lastIndex = label.LastIndexOf('(');
			if (lastIndex == -1)
				lastIndex = label.LastIndexOfAny(new[] { '.', ' ' });
			else
				lastIndex = label.Substring(0, lastIndex).LastIndexOfAny(new[] { '.', ' ' });
			if (lastIndex != -1)
				return label.Substring(lastIndex + 1);
			return label;
		}

		static string ShortenEdgeLabel(string label)
		{
			string[] methods = label.Split(new[] { " > " }, StringSplitOptions.None);
			return ShortenMethodLabel(methods[0]) + "\\n" + ShortenMethodLabel(methods[1]);
		}

		static GmlNode FindCommonDominator(
			IBidirectionalGraph<GmlNode, GmlEdge> graph,
			GmlNode a,
			GmlNode b)
		{
			var dominators = new Dictionary<GmlNode, HashSet<GmlNode>>();
			var allNodes = new HashSet<GmlNode>(graph.Vertices);
			var workList = new HashSet<GmlNode>();

			foreach (var node in graph.Vertices)
			{
				if (node.IsRoot)
				{
					dominators[node] = new HashSet<GmlNode> { node };
				}
				else
				{
					dominators[node] = allNodes;
					workList.Add(node);
				}
			}

			while (workList.Count > 0)
			{
				var node = workList.First();
				workList.Remove(node);
				var newDom = new HashSet<GmlNode>(allNodes);
				foreach (var predecessor in graph.InEdges(node).Select(e => e.Source))
					newDom.IntersectWith(dominators[predecessor]);
				newDom.Add(node);
				if (!dominators[node].SetEquals(newDom))
				{
					dominators[node] = newDom;
					foreach (var successor in graph.InEdges(node).Select(e => e.Source).Where(n => !n.IsRoot))
						workList.Add(successor);
				}
			}

			return dominators[a].Intersect(dominators[b]).FirstOrDefault();
		}

		static void Main(string[] args)
		{
			if (args.Length < 1)
			{
				Console.Out.WriteLine("Usage: {0} <graph>", Environment.CommandLine);
				return;
			}

			using (StreamReader reader = new StreamReader(args[0]))
			{
				var gml = GmlParser.GmlParser.ParseGml(reader);
				var graph = GmlParser.GmlParser.LoadGml(gml);

				var dfs = new DepthFirstSearchAlgorithm<GmlNode, GmlEdge>(graph);
				var treeEdges = new List<GmlEdge>();
				var fwdEdges = new List<GmlEdge>();
				var backEdges = new List<GmlEdge>();
				dfs.BackEdge += e => backEdges.Add(e);
				dfs.TreeEdge += e => treeEdges.Add(e);
				dfs.ForwardOrCrossEdge += e => fwdEdges.Add(e);
				dfs.Compute();
				int lockGraphId = 0;
				foreach (var backEdge in backEdges)
				{
					var frontEdge = graph.Edges.Where(e => backEdge.Source == e.Target && backEdge.Target == e.Source).FirstOrDefault();
					if (frontEdge != null)
					{
						var vertices = new Queue<GmlNode>();
						var leadingGraph = new BidirectionalGraph<GmlNode, GmlEdge>();

						if (!backEdge.Source.IsRoot)
						{
							vertices.Enqueue(backEdge.Source);
							leadingGraph.AddVertex(backEdge.Source);
						}
						if (!backEdge.Target.IsRoot)
						{
							vertices.Enqueue(backEdge.Target);
							leadingGraph.AddVertex(backEdge.Source);
						}
						while (vertices.Count > 0)
						{
							var vertex = vertices.Dequeue();
							foreach (var leadingEdge in treeEdges.Union(fwdEdges).Where(e => e.Target == vertex && e != frontEdge))
							{
								leadingGraph.AddVerticesAndEdge(leadingEdge);
								if (!leadingEdge.Source.IsRoot)
									vertices.Enqueue(leadingEdge.Source);
							}
						}

						// Skip over guard locks!
						if (leadingGraph.ContainsVertex(backEdge.Source) && leadingGraph.ContainsVertex(backEdge.Target))
						{
							var guardLock = FindCommonDominator(leadingGraph, backEdge.Source, backEdge.Target);
							if (guardLock != null)
								continue;
						}
				
						using (var writer = new StreamWriter("lock." + lockGraphId + ".dott"))
						{
							writer.WriteLine("digraph {");
							writer.WriteLine(" \"{0}\" [color=red]", frontEdge.Source);
							writer.WriteLine(" \"{0}\" [color=red]", frontEdge.Target);
							writer.WriteLine(" \"{0}\" -> \"{1}\" [color=red,label=\"{2}\"]", frontEdge.Source, frontEdge.Target, ShortenEdgeLabel(frontEdge.Label));
							writer.WriteLine(" \"{0}\" -> \"{1}\" [color=red,label=\"{2}\"]", backEdge.Source, backEdge.Target, ShortenEdgeLabel(backEdge.Label));
							foreach (var leadingEdge in leadingGraph.Edges)
								writer.WriteLine(" \"{0}\" -> \"{1}\" [label=\"{2}\"]", leadingEdge.Source, leadingEdge.Target, ShortenEdgeLabel(leadingEdge.Label));
							writer.WriteLine("}");
						}
						lockGraphId++;
					}
				}
			}
		}
	}
}
