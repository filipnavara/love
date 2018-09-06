using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GmlParser
{
	public class GmlNode
	{
		public int Id { get; set; }
		public string Label { get; set; }
		public bool IsRoot { get; set; }
		public override string ToString()
		{
			return Label;
		}
	}
}
