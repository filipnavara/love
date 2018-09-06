// Characteristics of test:
// - Lock on field objects
// - lock (a) lock (b) vs. lock (b) lock (a) pattern on different threads
// - Threads constructed new Thread(new ParametrizedThreadStart())

using System;
using System.Threading;

public class Deadlock
{
	static readonly object a = new object();
	static readonly object b = new object();

	public static void FunctionA(object state)
	{
		if ((bool)state)
		{
			lock (b)
			{
				lock (a)
				{
				}
			}
		}
		else
		{
			lock (a)
			{
				lock (b)
				{
				}
			}
		}
	}

	public static void Main()
	{
		new Thread(FunctionA).Start(true);
		new Thread(FunctionA).Start(false);
	}
}
