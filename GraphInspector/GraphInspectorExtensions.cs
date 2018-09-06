using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QuickGraph;

namespace GraphInspector
{
	public static class GraphInspectorExtensions
	{
		public static void ShowGraphInspector<TVertex, TEdge>(
			this IBidirectionalGraph<TVertex, TEdge> graph,
			TVertex startVertex,
			VertexIdentity<TVertex> formatFunction)
			where TEdge : IEdge<TVertex>
		{
			new GraphInspectorForm<TVertex, TEdge>(graph, startVertex, formatFunction).ShowDialog();
		}
	}
}
