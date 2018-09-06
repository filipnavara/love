using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QuickGraph;
using QuickGraph.Algorithms;

namespace Love
{
	public static class GmlWriter
	{
		public static void DumpGraph<N, E>(
			string fileName,
			string comment,
			IBidirectionalGraph<N, E> lockGraph,
			ISet<N> roots,
			Func<N, string> getNodeLabel,
			Func<E, string> getEdgeLabel)
			where E : IEdge<N>
		{
			using (var writer = new System.IO.StreamWriter(fileName))
			{
				writer.WriteLine("graph [");
				writer.WriteLine(" comment \"" + comment.Replace('&', '$') + "\"");
				writer.WriteLine(" directed 1");
				var vertexIdentity = lockGraph.GetVertexIdentity();
				foreach (var vertex in lockGraph.Vertices)
				{
					if (roots.Contains(vertex))
						writer.WriteLine(" node [ id {0} label \"{1}\" root 1 ]", vertexIdentity(vertex), getNodeLabel(vertex).Replace('&', '$'));
					else
						writer.WriteLine(" node [ id {0} label \"{1}\" ]", vertexIdentity(vertex), getNodeLabel(vertex).Replace('&', '$'));
				}
				foreach (var edge in lockGraph.Edges)
				{
					writer.WriteLine(" edge [ source {0} target {1} label \"{2}\" ]",
						vertexIdentity(edge.Source),
						vertexIdentity(edge.Target),
						getEdgeLabel(edge));
				}
				writer.WriteLine("]");
			}
		}
	}
}
