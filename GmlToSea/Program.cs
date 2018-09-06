//#define USE_GRAPH_INSPECTOR
//#define USE_GML_OUTPUT
//#define USE_GRAPHML_OUTPUT
//#define USE_DOT_OUTPUT
#define USE_SEA_OUTPUT

using System;
using System.IO;
using System.Xml;
using GraphInspector;
using Mono.Cecil;
using QuickGraph.Algorithms;
using QuickGraph.Graphviz;
using QuickGraph.Serialization;
using StaticAnalysis.CallGraph;
using StaticAnalysis;
using GmlParser;

namespace GmlToSea
{
	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length < 1)
			{
				Console.Out.WriteLine("Usage: {0} <graph>", Environment.CommandLine);
				return;
			}

			using (var reader = new StreamReader(args[0]))
			{
				var gml = GmlParser.GmlParser.ParseGml(reader);
				var graph = GmlParser.GmlParser.LoadGml(gml);

#if USE_GRAPH_INSPECTOR
				callGraph.ShowGraphInspector(
					assembly.EntryPoint,
					(v) => v.ToString());
#endif
#if USE_GRAPHML_OUTPUT
				using (var xwriter = XmlWriter.Create("graph.graphml"))
				{
					GraphMLExtensions.SerializeToGraphML(
						callGraph, xwriter,
						AlgorithmExtensions.GetVertexIdentity(callGraph),
						AlgorithmExtensions.GetEdgeIdentity(callGraph));
				}
#endif
#if USE_DOT_OUTPUT
				var graphviz = new GraphvizAlgorithm<MethodDefinition, CallGraphEdge>(callGraph);
				graphviz.FormatVertex += new FormatVertexEventHandler<MethodDefinition>(
					(sender, e) => e.VertexFormatter.Label = e.Vertex.FullName);
				string output = graphviz.Generate(new FileDotEngine(), "graph.dot");
#endif
#if USE_SEA_OUTPUT
				using (var writer = new System.IO.StreamWriter(Path.GetFileNameWithoutExtension(args[0]) + ".sea"))
				{
					SeaWriter<GmlNode, GmlEdge>.Write(
						writer,
						graph,
						() => new GmlNode { Id = Int32.MaxValue, IsRoot = true, Label = "FakeRoot" },
						(a, b) => new GmlEdge(a, b));
				}
#endif
			}
		}
	}
}
