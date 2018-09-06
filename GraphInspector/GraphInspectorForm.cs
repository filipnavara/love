using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using QuickGraph;

namespace GraphInspector
{
	public partial class GraphInspectorForm<TVertex, TEdge> : Form
		where TEdge : IEdge<TVertex>
	{
		IBidirectionalGraph<TVertex, TEdge> graph;
		List<Tuple<TVertex, Rectangle>> vertexPositions;
		List<Tuple<Point, Point>> arrows;
		VertexIdentity<TVertex> formatFunction;
		TVertex currentVertex;
		int totalWidth, totalHeight;

		public GraphInspectorForm(
			IBidirectionalGraph<TVertex, TEdge> graph,
			TVertex startVertex,
			VertexIdentity<TVertex> formatFunction)
		{
			InitializeComponent();
			vertexPositions = new List<Tuple<TVertex, Rectangle>>();
			arrows = new List<Tuple<Point, Point>>();

			this.graph = graph;
			this.currentVertex = startVertex;
			this.formatFunction = formatFunction;
			Relayout();
		}

		private void DrawVertex(Graphics g, Rectangle bounds, string vertex)
		{
			g.FillRectangle(SystemBrushes.Window, bounds);
			g.DrawRectangle(SystemPens.WindowFrame, bounds);
			TextRenderer.DrawText(g, vertex, this.Font, new Point(bounds.Left + 8, bounds.Top + 4), this.ForeColor);
		}

		private void Relayout()
		{
			int predecesorsWidth = 0, successorsWidth = 0, currentWidth = 0, totalWidth = 0;
			IEnumerable<TEdge> predecesors, successors;

			// Calculate the width of the drawing
			if (graph.TryGetInEdges(currentVertex, out predecesors))
			{
				foreach (TEdge predecesor in predecesors)
				{
					predecesorsWidth += TextRenderer.MeasureText(formatFunction(predecesor.Source), this.Font).Width;
					// Padding of single node
					predecesorsWidth += 16;
					// Distance between nodes
					predecesorsWidth += 16;
				}
				predecesorsWidth -= 16;
			}
			currentWidth = TextRenderer.MeasureText(formatFunction(currentVertex), this.Font).Width + 16;
			if (graph.TryGetOutEdges(currentVertex, out successors))
			{
				foreach (TEdge successor in successors)
				{
					successorsWidth += TextRenderer.MeasureText(formatFunction(successor.Target), this.Font).Width;
					// Padding of single node
					successorsWidth += 16;
					// Distance between nodes
					successorsWidth += 16;
				}
				successorsWidth -= 16;
			}
			totalWidth = Math.Max(Math.Max(predecesorsWidth, successorsWidth), currentWidth);

			vertexPositions.Clear();
			arrows.Clear();

			Rectangle currentVertexBounds = new Rectangle((totalWidth - currentWidth) / 2 + 6, 40, TextRenderer.MeasureText(formatFunction(currentVertex), this.Font).Width + 16, 20);
			vertexPositions.Add(Tuple.Create(currentVertex, currentVertexBounds));
			if (predecesors != null)
			{
				int left = (totalWidth - predecesorsWidth) / 2 + 6;
				foreach (TEdge predecesor in predecesors)
				{
					Rectangle vertexBounds = new Rectangle(left, 6, TextRenderer.MeasureText(formatFunction(predecesor.Source), this.Font).Width + 16, 20);
					left = vertexBounds.Right + 16;
					vertexPositions.Add(Tuple.Create(predecesor.Source, vertexBounds));
					arrows.Add(Tuple.Create(
						new Point((vertexBounds.Right + vertexBounds.Left) / 2, 26),
						new Point((currentVertexBounds.Right + currentVertexBounds.Left) / 2, 40)));
				}
			}
			if (successors != null)
			{
				int left = (totalWidth - successorsWidth) / 2 + 6;
				foreach (TEdge successor in successors)
				{
					Rectangle vertexBounds = new Rectangle(left, 74, TextRenderer.MeasureText(formatFunction(successor.Target), this.Font).Width + 16, 20);
					left = vertexBounds.Right + 16;
					vertexPositions.Add(Tuple.Create(successor.Target, vertexBounds));
					arrows.Add(Tuple.Create(
						new Point((vertexBounds.Right + vertexBounds.Left) / 2, 74),
						new Point((currentVertexBounds.Right + currentVertexBounds.Left) / 2, 60)));
				}
			}

			this.totalWidth = totalWidth + 12;
			this.totalHeight = 100;
			AdjustScrollbars();
		}

		private void AdjustScrollbars()
		{
			this.AutoScroll = true;
			this.AutoScrollMinSize = new Size(totalWidth, totalHeight);
		}

		private void GraphInspectorForm_Paint(object sender, PaintEventArgs e)
		{
			foreach (var rectAndVertex in vertexPositions)
			{
				Rectangle r = rectAndVertex.Item2;
				r.Offset(-this.HorizontalScroll.Value, -this.VerticalScroll.Value);
				DrawVertex(e.Graphics, r, formatFunction(rectAndVertex.Item1));
			}
			foreach (var arrow in arrows)
			{
				Point p1 = arrow.Item1, p2 = arrow.Item2;
				p1.Offset(-this.HorizontalScroll.Value, -this.VerticalScroll.Value);
				p2.Offset(-this.HorizontalScroll.Value, -this.VerticalScroll.Value);
				e.Graphics.DrawLine(SystemPens.WindowFrame, p1, p2);
			}
		}

		private void GraphInspectorForm_MouseClick(object sender, MouseEventArgs e)
		{
			Point p = e.Location;
			p.Offset(this.HorizontalScroll.Value, this.VerticalScroll.Value);
			foreach (var rectAndVertex in vertexPositions)
			{
				if (rectAndVertex.Item2.Contains(p))
				{
					currentVertex = rectAndVertex.Item1;
					Relayout();
					Invalidate();
					return;
				}
			}
		}

		private void GraphInspectorForm_Resize(object sender, EventArgs e)
		{
			AdjustScrollbars();
		}
	}
}
