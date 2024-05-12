using BenchmarkDotNet.Running;

namespace RedaFastaBenchmarks;
class Program
{
	static void Main(string[] args)
	{

		BenchmarkRunner.Run<RedaFastaBase>();
	}
}