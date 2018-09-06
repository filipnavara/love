// Characteristics of test:
// - Lock on static objects
// - lock (a) lock (b) vs. lock (b) lock (a) pattern on different threads
// - Threads constructed ThreadPool.QueueUserWorkItem(new WaitCallback())

using System;
using System.Threading;

public class Deadlock
{
	static readonly object a = new object();
	static readonly object b = new object();

	public static void FunctionA(object state)
	{
		lock (b)
		{
			lock (a)
			{
			}
		}
	}

	public static void FunctionB(object state)
	{
		lock (a)
		{
			lock (b)
			{
			}
		}
	}

	public static void Main()
	{
		ThreadPool.QueueUserWorkItem(FunctionA);
		ThreadPool.QueueUserWorkItem(FunctionB);
	}
}
