using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using StaticAnalysis;

namespace Love.IntraproceduralAnalysis
{
	/// <summary>
	/// Represents a symbolic object on the heap.
	/// </summary>
	public class HeapObject
	{
		/// <summary>
		/// Initializes a new heap object with given program point and object type.
		/// </summary>
		/// <param name="programPoint">Program point where the heap object was created</param>
		/// <param name="type">Type of the object that this symbolic heap object represents</param>
		public HeapObject(ProgramPoint programPoint, TypeReference type)
		{
			if (type == null)
				throw new ArgumentNullException("type");
			this.ProgramPoint = programPoint;
			this.Type = type;
		}

		/// <summary>
		/// Program point where the heap object was created or passed (in case of
		/// parameters during intra-procedural analysis).
		/// </summary>
		public ProgramPoint ProgramPoint { get; private set; }

		/// <summary>
		/// Type of the object that the symbolic heap object represents.
		/// </summary>
		public TypeReference Type { get; private set; }

		/// <summary>
		/// Determines whether the specified Object is equal to the current Object.
		/// </summary>
		/// <param name="obj">The object to compare with the current object.</param>
		/// <returns>true if the specified Object is equal to the current Object; otherwise, false.</returns>
		public override bool Equals(object obj)
		{
			var other = obj as HeapObject;
			if (other == null)
				return false;
			return Equals(ProgramPoint, other.ProgramPoint) && TypeExtensions.TypeMatch(Type, other.Type);
		}

		/// <summary>
		/// Serves as a hash function for a particular type.
		/// </summary>
		/// <returns>A hash code for the current HeapObject.</returns>
		public override int GetHashCode()
		{
			return unchecked((ProgramPoint == null ? 0 : ProgramPoint.GetHashCode()) + Type.GetHashCode());
		}

		/// <summary>
		/// Get a text representation of the symbolic heap object.
		/// </summary>
		/// <returns>Text representation of the symbolic heap object.</returns>
		public override string ToString()
		{
			return String.Format("instruction {0} type {1}",
				ProgramPoint == null ? "<null>" : ProgramPoint.ToString(),
				Type.ToString());
		}
	}
}
