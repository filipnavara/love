using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using QuickGraph;

namespace GmlParser
{
	public class GmlParser
	{
		public static KeyValuePair<string, object> ParseGml(StreamReader reader)
		{
			int ch;
			StringBuilder name = new StringBuilder();
			while ((ch = reader.Read()) != -1 && Char.IsWhiteSpace((char)ch))
				;
			name.Append((char)ch);
			while ((ch = reader.Read()) != -1 && !Char.IsWhiteSpace((char)ch))
				name.Append((char)ch);
			while ((ch = reader.Read()) != -1 && Char.IsWhiteSpace((char)ch))
				;

			switch (ch)
			{
				case '"':
					StringBuilder textValue = new StringBuilder();
					while ((ch = reader.Read()) != -1 && ch != '"')
						textValue.Append((char)ch);
					return new KeyValuePair<string, object>(name.ToString(), textValue.ToString());
				case '[':
					List<KeyValuePair<string, object>> listValue = new List<KeyValuePair<string, object>>();
					while (true)
					{
						while ((ch = reader.Peek()) != -1 && Char.IsWhiteSpace((char)ch))
							reader.Read();
						if (ch == ']')
						{
							reader.Read();
							break;
						}
						listValue.Add(ParseGml(reader));
					}
					return new KeyValuePair<string, object>(name.ToString(), listValue);
				default:
					StringBuilder integerValue = new StringBuilder();
					integerValue.Append((char)ch);
					while ((ch = reader.Read()) != -1 && Char.IsDigit((char)ch))
						integerValue.Append((char)ch);
					return new KeyValuePair<string, object>(name.ToString(), int.Parse(integerValue.ToString()));
			}
		}

		public static IBidirectionalGraph<GmlNode, GmlEdge> LoadGml(KeyValuePair<string, object> gml)
		{
			var graph = new BidirectionalGraph<GmlNode, GmlEdge>();
			var idToNode = new Dictionary<int, GmlNode>();

			foreach (KeyValuePair<string, object> kv in (List<KeyValuePair<string, object>>)gml.Value)
			{
				if (kv.Key == "node")
				{
					int id = -1;
					string label = String.Empty;
					int root = 0;
					foreach (KeyValuePair<string, object> nodeKv in (List<KeyValuePair<string, object>>)kv.Value)
					{
						if (nodeKv.Key == "id")
							id = (int)nodeKv.Value;
						else if (nodeKv.Key == "label")
							label = (string)nodeKv.Value;
						else if (nodeKv.Key == "root")
							root = (int)nodeKv.Value;
					}
					var node = new GmlNode { Id = id, Label = label, IsRoot = root > 0 };
					idToNode[id] = node;
					graph.AddVertex(node);
				}
				else if (kv.Key == "edge")
				{
					int source = -1;
					int target = -1;
					string label = String.Empty;
					foreach (KeyValuePair<string, object> nodeKv in (List<KeyValuePair<string, object>>)kv.Value)
					{
						if (nodeKv.Key == "source")
							source = (int)nodeKv.Value;
						else if (nodeKv.Key == "target")
							target = (int)nodeKv.Value;
						else if (nodeKv.Key == "label")
							label = (string)nodeKv.Value;
					}
					graph.AddEdge(new GmlEdge(idToNode[source], idToNode[target]) { Label = label });
				}
			}

			return graph;
		}
	}
}
