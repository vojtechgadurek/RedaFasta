using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedaFastaTest
{
	static class FileData
	{

		static public List<(string file, string[] kMers)> Data = new List<(string, string[])>();
		static FileData()
		{
			Data.Add((">small3 k=2 l=30\nCaaaCaaaaaaaaaaaTaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n", new string[] { "CA", "CA", "TA" }));

			Data.Add((">small3 k=2 l=90\nCaaaCaaaaaaaaaaaaaaaaaaaaaaaTaCaaaCaaaaaaaaaaaaaaaaaaaaaaaTaCaaaCaaaaaaaaaaaaaaaaaaaaaaaTa\n", new string[] { "CA", "CA", "TA", "CA", "CA", "TA", "CA", "CA", "TA" }));



			Data.Add((">small3 k=1 l=4\nCCCC\n", new string[] { "C", "C", "C", "C" }));

			Data.Add((">small3 k=2 l=3\nGGG\n", new string[] { "CC", "CC" }));





			Data.Add((">small3 k=2 l=30\nCaaaCaaaaaaaaaaaaaaaaaaaaaaaTa\n", new string[] { "CA", "CA", "TA" }));


			Data.Add((">small3 k=2 l=3\naTa\n", new string[] { "TA", }));
			Data.Add((">small3 k=2 l=3\nAta\n", new string[] { "AT", }));




			Data.Add((">small3 k=2 l=3\naGc\n", new string[] { "GC", }));

			Data.Add((">small3 k=2 l=3\naCg\n", new string[] { "CG", }));




			Data.Add((">small3 k=3 l=6\nAaaAaa\n", new string[] { "AAA", "AAA" }));

			Data.Add((">small3 k=2 l=5\nAaaaa\n", new string[] { "AA" }));
			Data.Add((">small3 k=3 l=6\nCccCcc\n", new string[] { "CCC", "CCC" }));



			Data.Add((">small3 k=2 l=5\nGAggG\n", new string[] { "GA", "AG" }));


			Data.Add((">small1 k=1 l=1\nA\n", new string[] { "A" }));

			Data.Add((">small3 k=3 l=6\nAcgAcg\n", new string[] { "ACG", "ACG" }));

			Data.Add((">small3 k=3 l=3\nACG\n", new string[] { "ACG" }));



			Data.Add((">small3 k=3 l=0\nACGaaaAcg\n", new string[] { }));






			Data.Add((">small3 k=2 l=5\nGgggG\n", new string[] { "CC" }));




			Data.Add((">small1 k=1 l=5\nAAAAA\n", new string[] { "A", "A", "A", "A", "A" }));
			Data.Add((">small2 k=1 l=5\nTTTTT\n", new string[] { "A", "A", "A", "A", "A" }));

			Data.Add((">small3 k=1 l=5\nCCCCC\n", new string[] { "C", "C", "C", "C", "C" }));
			Data.Add((">small3 k=1 l=5\nGGGGG\n", new string[] { "C", "C", "C", "C", "C" }));

			Data.Add((">small3 k=1 l=5\nGgggG\n", new string[] { "C", "C" }));


			Data.Add((">small3 k=2 l=10\naTaaaaaaaaaa\n", new string[] { "TA", }));

			Data.Add((">small3 k=2 l=10\nCaaCaaTaaaaaaaaaa\n", new string[] { "CA", "CA", "TA" }));
		}
	}
}
