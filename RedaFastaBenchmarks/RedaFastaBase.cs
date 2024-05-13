using BenchmarkDotNet.Attributes;
using RedaFasta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedaFastaBenchmarks
{
	public class RedaFastaBase
	{
		[GlobalSetup]
		public void SetUp()
		{
			StreamWriter writer = new StreamWriter("test.test");
			writer.WriteLine(">small1 k=31 l=100000000");
			writer.WriteLine(RandomString(100_000_000));
			string RandomString(int length)
			{
				const string chars = "ACGT";

				StringBuilder stringBuilder = new StringBuilder(length);
				for (int i = 0; i < length; i++)
				{
					stringBuilder.Append(chars[new Random().Next(chars.Length)]);
				}
				return stringBuilder.ToString();
			}
			writer.Close();
		}

		[Benchmark]
		public int Borrowing()
		{
			var textReader = new StreamReader("test.test");
			var config = FastaFile.Open(textReader);

			var fastaFileReader = new FastaFileReader(config.kMerSize, config.nCharsInFile, textReader, 1024 * 1024);

			ulong[] kMerBuffer = new ulong[1024 * 1024];

			int returned = 0;
			ulong sum = 0;
			while (true)
			{
				var returnedNow = fastaFileReader.BorrowBuffer();
				if (returnedNow == null) break;
				returned += returnedNow.Size - returnedNow.Used;
				fastaFileReader.RecycleBuffer(returnedNow);
			}

			if (returned != 100_000_000 - 30) throw new Exception($"Returned is not equal to 100_000_000 {returned}");
			fastaFileReader.Dispose();
			return (int)sum;
		}



		[Benchmark]
		public int BaseTest()
		{
			var textReader = new StreamReader("test.test");
			var config = FastaFile.Open(textReader);

			var fastaFileReader = new FastaFileReader(config.kMerSize, config.nCharsInFile, textReader, 1024 * 1024);

			ulong[] kMerBuffer = new ulong[1024 * 1024];

			int returned = 0;
			ulong sum = 0;
			while (true)
			{
				var returnedNow = fastaFileReader.FillBuffer(kMerBuffer);
				if (returnedNow == 0) break;
				returned += returnedNow;
			}

			if (returned != 100_000_000 - 30) throw new Exception($"Returned is not equal to 100_000_000 {returned}");
			fastaFileReader.Dispose();
			return (int)sum;
		}

		[Benchmark]
		public int TranslatorTest()
		{
			var textReader = new StreamReader("test.test");
			var config = FastaFile.Open(textReader);

			var fastaFileReader = new FastaFileReader(config.kMerSize, config.nCharsInFile, textReader, init: false);

			ulong[] kMerBuffer = new ulong[1024 * 1024];

			int returned = 0;
			ulong sum = 0;

			char[] buffer = new char[1024 * 1024];
			while (true)
			{
				var c = textReader.Read(buffer);
				var returnedNow = fastaFileReader.Translate(kMerBuffer, buffer, 0, c);
				if (returnedNow == 0) break;
				returned += returnedNow;
			}

			fastaFileReader.Dispose();
			return (int)sum;
		}


		[GlobalCleanup]
		public void Cleanup()
		{
			File.Delete("test.test");
		}
	}
}
