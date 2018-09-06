using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QuickGraph;

namespace GmlParser
{
	public class GmlEdge : Edge<GmlNode>
	{
		public GmlEdge(GmlNode source, GmlNode target)
			: base(source, target)
		{
		}

		public string Label { get; set; }
	}
}
