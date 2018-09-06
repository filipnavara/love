using System;
using System.Diagnostics;
using StaticAnalysis.CallGraph;
using Mono.Cecil;
using System.Collections.Generic;
using System.IO;

namespace Love
{
	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length < 1)
			{
				Console.Out.WriteLine("Usage: {0} <assembly> [options]", Environment.CommandLine);
				return;
			}

			AnalysisOptions options = AnalysisOptions.DumpLockGraph;
			for (int arg = 1; arg < args.Length; arg++)
			{
				if (args[arg] == "--noaliasing")
					options |= AnalysisOptions.NoAliasing;
				else if (args[arg] == "--noaliasingaftermerge")
					options |= AnalysisOptions.NoAliasingAfterMerge;
				else if (args[arg] == "--ignoresystemnamespace")
					options |= AnalysisOptions.IgnoreSystemNamespace;
				else if (args[arg] == "--callgraph=dumb")
					options |= AnalysisOptions.DumbCallGraph;
			}

			var stopwatch = new Stopwatch();
			stopwatch.Start();

			Console.Out.Write("Loading assembly...");
			var assembly = AssemblyDefinition.ReadAssembly(args[0]);
			Console.Out.WriteLine("done");

			Console.Out.Write("Building call graph...");
			CallGraph callGraph;
			if ((options & AnalysisOptions.DumbCallGraph) != 0)
				callGraph = new DumbCallGraphBuilder(assembly.EntryPoint).Build();
			else
				callGraph = new ChaCallGraphBuilder(assembly.EntryPoint).Build();
			GmlWriter.DumpGraph(
				Path.GetFileNameWithoutExtension(args[0]) + ".callgraph.gml", "", callGraph.QuickGraph, new HashSet<MethodDefinition>(),
				n => n.ToString(),
				e => e.ProgramPoint.Offset.ToString());
			Console.Out.WriteLine("done");

			Console.Out.Write("Building lock graph...");
			var lockGraph = new LockGraph(callGraph, assembly.EntryPoint, options);
			GmlWriter.DumpGraph(
				Path.GetFileNameWithoutExtension(args[0]) + ".lockgraph.gml", "", lockGraph.Graph, lockGraph.Roots,
				n => n.ToString(),
				e => e.SourceProgramPoint.ToString().Replace('&', '$') + " > " + e.TargetProgramPoint.ToString().Replace('&', '$'));
			Console.Out.WriteLine("done");
			
			stopwatch.Stop();
			Console.Out.WriteLine();
			Console.Out.WriteLine("Running time: " + stopwatch.Elapsed);
		}
	}
}
