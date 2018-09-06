using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using QuickGraph;
using QuickGraph.Algorithms;
using QuickGraph.Algorithms.Search;
using QuickGraph.Algorithms.Observers;

namespace GmlToSea
{
	static class SeaWriter<TVertex, TEdge>
		where TEdge : IEdge<TVertex>
	{
		public static void WriteAttributes(
			StreamWriter writer,
			string attributeName,
			string attributeType,
			string attributeDefault,
			IEnumerable<Tuple<string, string>> nodeValues,
			IEnumerable<Tuple<string, string>> linkValues)
		{
			writer.WriteLine("    {");
			writer.WriteLine("      @name=$" + attributeName + ";");
			writer.WriteLine("      @type=" + attributeType + ";");
			writer.WriteLine("      @default=" + attributeDefault + ";");
			writer.WriteLine("      @nodeValues=[");
			if (nodeValues != null)
				writer.WriteLine(String.Join(",\r\n", nodeValues.Select((t) => "      { @id=" + t.Item1 + "; @value=" + t.Item2 + "; }")));
			writer.WriteLine("      ];");
			writer.WriteLine("      @linkValues=[");
			if (linkValues != null)
				writer.WriteLine(String.Join(",\r\n", linkValues.Select((t) => "      { @id=" + t.Item1 + "; @value=" + t.Item2 + "; }")));
			writer.WriteLine("      ];");
			writer.WriteLine("      @pathValues=[];");
			writer.WriteLine("    }");
		}

		public static void Write(
			StreamWriter writer,
			IBidirectionalGraph<TVertex, TEdge> graph,
			Func<TVertex> createFakeVertex,
			Func<TVertex, TVertex, TEdge> createEdge)
		{
			var roots = new List<TVertex>(graph.Roots());//.Except(isolatedVertices);

			var edgeRecorder = new EdgeRecorderObserver<TVertex, TEdge>();
			var algo = new BreadthFirstSearchAlgorithm<TVertex, TEdge>(graph);
			using (edgeRecorder.Attach(algo))
				algo.Compute();
			HashSet<TVertex> targetVertices = new HashSet<TVertex>();
			foreach (var edge in edgeRecorder.Edges)
				targetVertices.Add(edge.Target);
			foreach (var root in roots)
				targetVertices.Add(root);
			// Fix for orphan cycles
			foreach (var vertex in graph.Vertices)
			{
				if (!targetVertices.Contains(vertex))
				{
					roots.Add(vertex);
					targetVertices.Add(vertex);
					algo = new BreadthFirstSearchAlgorithm<TVertex, TEdge>(graph);
					using (edgeRecorder.Attach(algo))
						algo.Compute(vertex);
					foreach (var edge in edgeRecorder.Edges)
						targetVertices.Add(edge.Target);
				}
			}

			BidirectionalGraph<TVertex, TEdge> newGraph = new BidirectionalGraph<TVertex, TEdge>();
			newGraph.AddVertexRange(graph.Vertices);
			newGraph.AddEdgeRange(graph.Edges);
			TVertex fakeRootVertex = createFakeVertex();
			newGraph.AddVertex(fakeRootVertex);
			foreach (var root in roots)
				newGraph.AddEdge(createEdge(fakeRootVertex, root));
			roots.Clear();
			roots.Add(fakeRootVertex);
			edgeRecorder = new EdgeRecorderObserver<TVertex, TEdge>();
			algo = new BreadthFirstSearchAlgorithm<TVertex, TEdge>(newGraph);
			using (edgeRecorder.Attach(algo))
				algo.Compute();
			graph = newGraph;

			writer.WriteLine("Graph");
			writer.WriteLine("{");
			writer.WriteLine("  @name=\"CallGraph\";");
			writer.WriteLine("  @description=\"\";");
			writer.WriteLine("  @numNodes=" + (graph.VertexCount /*- isolatedVertices.Length*/ + 1) + ";");
			writer.WriteLine("  @numLinks=" + (graph.EdgeCount + roots.Count) + ";");
			writer.WriteLine("  @numPaths=0;");
			writer.WriteLine("  @numPathLinks=0;");
			writer.WriteLine("  @links=[");
			bool first = true;
			var vertexIds = new Dictionary<TVertex, string>(graph.VertexCount);
			string vertexId, vertexId2;
			var edgeIds = new Dictionary<TEdge, string>(graph.EdgeCount);
			foreach (var edge in graph.Edges)
			{
				if (first)
					first = false;
				else
					writer.WriteLine(",");
				if (!vertexIds.TryGetValue(edge.Source, out vertexId))
					vertexIds[edge.Source] = vertexId = vertexIds.Count.ToString();
				if (!vertexIds.TryGetValue(edge.Target, out vertexId2))
					vertexIds[edge.Target] = vertexId2 = vertexIds.Count.ToString();
				writer.Write("    { @source=" + vertexId + "; @destination=" + vertexId2 + "; }");
				if (!edgeIds.ContainsKey(edge))
					edgeIds[edge] = edgeIds.Count.ToString();
			}
			var fakeRootId = vertexIds.Count.ToString();
			var rootEdges = new List<string>();
			foreach (var root in roots)
			{
				writer.WriteLine(",");
				if (!vertexIds.TryGetValue(root, out vertexId))
					vertexIds[root] = vertexId = vertexIds.Count.ToString();
				writer.Write("    { @source=" + fakeRootId + "; @destination=" + vertexId + "; }");
				rootEdges.Add((edgeIds.Count + rootEdges.Count).ToString());
			}

			writer.WriteLine();
			writer.WriteLine("  ];");
			writer.WriteLine("  @paths=;");
			writer.WriteLine("  @enumerations=;");
			writer.WriteLine("  @attributeDefinitions=[");
			WriteAttributes(writer, "root", "bool", "|| false ||", new[] { Tuple.Create(fakeRootId, "T") }/*roots.Select((root) => Tuple.Create(vertexIds[root], "T"))*/, null);
			writer.WriteLine(",");

			WriteAttributes(writer, "tree_link", "bool", "|| false ||", null,
				edgeRecorder.Edges.Select((edge) => Tuple.Create(edgeIds[edge], "T")).Union(
				rootEdges.Select((e) => Tuple.Create(e, "T"))));

			writer.WriteLine();
			writer.WriteLine("  ];");
			writer.WriteLine("  @qualifiers=[");
			writer.WriteLine("    {");
			writer.WriteLine("       @type=$spanning_tree;");
			writer.WriteLine("       @name=$sample_spanning_tree;");
			writer.WriteLine("       @description=;");
			writer.WriteLine("       @attributes=[");
			writer.WriteLine("         { @attribute=0; @alias=$root; },");
			writer.WriteLine("         { @attribute=1; @alias=$tree_link; }");
			writer.WriteLine("       ];");
			writer.WriteLine("    }");
			writer.WriteLine("  ];");
			writer.WriteLine("  @filters=;");
			writer.WriteLine("  @selectors=;");
			writer.WriteLine("  @displays=;");
			writer.WriteLine("  @presentations=;");
			writer.WriteLine("  @presentationMenus=;");
			writer.WriteLine("  @displayMenus=;");
			writer.WriteLine("  @selectorMenus=;");
			writer.WriteLine("  @filterMenus=;");
			writer.WriteLine("  @attributeMenus=;");
			writer.WriteLine("}");
		}
	}
}
