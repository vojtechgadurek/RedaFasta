using System.Text;

namespace RedaFastaTest
{
	public class FileReadTests
	{
		public static IEnumerable<object[]> GetData()
		{
			return FileData.Data.Select(x => new[] { (object)x });
		}
		[Theory]
		[MemberData(nameof(GetData))]
		public void SmallFileTest(object data)
		{
			//Tests on very small files < 1024 chars
			const int maxSize = 1024;

			(string file, string[] kMers) = ((string file, string[] kMers))data;


			var textReader = new StringReader(file);
			var config = FastaFile.Open(textReader);

			if (config.nCharsInFile > maxSize) throw new ArgumentException("File too big for this test");


			var fastaFileReader = new FastaFileReader(config.kMerSize, config.nCharsInFile, textReader, init: false);

			ulong[] kMerBuffer = new ulong[maxSize];
			char[] charBuffer = new char[maxSize];
			textReader.Read(charBuffer, 0, (int)config.nCharsInFile);

			var returned = fastaFileReader.Translate(kMerBuffer, charBuffer, 0, (int)config.nCharsInFile);

			for (int i = 0; i < kMers.Length; i++)
			{
				Assert.Equal(kMers[i], Utils.TranslateUlongToString(kMerBuffer[i], config.kMerSize));
			}
			Assert.Equal(kMers.Length, returned);

		}


		[Theory]
		[MemberData(nameof(GetData))]

		public void FastaFileReader(object data)
		{
			//Tests on very small files < 1024 chars

			const int maxSize = 1024;

			(string file, string[] kMers) = ((string file, string[] kMers))data;


			var textReader = new StringReader(file);
			var config = FastaFile.Open(textReader);

			if (config.nCharsInFile > maxSize) throw new ArgumentException("File too big for this test");


			var fastaFileReader = new FastaFileReader(config.kMerSize, config.nCharsInFile, textReader, 1, 1);

			ulong[] kMerBuffer = new ulong[maxSize];

			var returned = fastaFileReader.FillBuffer(kMerBuffer);



			for (int i = 0; i < returned; i++)
			{
				Assert.Equal(kMers[i], Utils.TranslateUlongToString(kMerBuffer[i], config.kMerSize));
			}

			Assert.Equal(kMers.Length, returned);

		}

		[Theory]
		[MemberData(nameof(GetData))]

		public void SmallCharBufferBiggerLocalBuffer(object data)
		{
			//Tests on very small files < 1024 chars

			const int maxSize = 1024;

			(string file, string[] kMers) = ((string file, string[] kMers))data;


			var textReader = new StringReader(file);
			var config = FastaFile.Open(textReader);

			if (config.nCharsInFile > maxSize) throw new ArgumentException("File too big for this test");


			var fastaFileReader = new FastaFileReader(config.kMerSize, config.nCharsInFile, textReader, 4, 1);

			ulong[] kMerBuffer = new ulong[1024];
			ulong[] exBuffer = new ulong[1];

			int count = 0;
			while (true)
			{
				var returned = fastaFileReader.FillBuffer(exBuffer);
				if (returned == 0) break;
				for (int i = 0; i < returned; i++)
				{
					kMerBuffer[count] = exBuffer[i];
					count++;
				}
			}

			for (int i = 0; i < kMers.Length; i++)
			{
				Assert.Equal(kMers[i], Utils.TranslateUlongToString(kMerBuffer[i], config.kMerSize));
			}
			Assert.Equal(kMers.Length, count);
		}

		[Theory]
		[MemberData(nameof(GetData))]

		public void SmallCharBufferBiggerLocalBufferMultipleBuffers(object data)
		{
			//Tests on very small files < 1024 chars

			const int maxSize = 1024;

			(string file, string[] kMers) = ((string file, string[] kMers))data;


			var textReader = new StringReader(file);
			var config = FastaFile.Open(textReader);

			if (config.nCharsInFile > maxSize) throw new ArgumentException("File too big for this test");


			var fastaFileReader = new FastaFileReader(config.kMerSize, config.nCharsInFile, textReader, 3, 7);

			ulong[] kMerBuffer = new ulong[1024];
			ulong[] exBuffer = new ulong[1];

			int count = 0;
			while (true)
			{
				var returned = fastaFileReader.FillBuffer(exBuffer);
				if (returned == 0) break;
				for (int i = 0; i < returned; i++)
				{
					kMerBuffer[count] = exBuffer[i];
					count++;
				}
			}

			for (int i = 0; i < kMers.Length; i++)
			{
				Assert.Contains(Utils.TranslateUlongToString(kMerBuffer[i], config.kMerSize), kMers);
			}
			Assert.Equal(kMers.Length, count);
		}


	}


	public class RedaFastaBase
	{
		public void SetUp()
		{
			StreamWriter writer = new StreamWriter("test.test");
			writer.WriteLine(">small1 k=31 l=1000000");
			writer.WriteLine(RandomString(1_000_000));
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

		[Fact]
		public int BaseTest()
		{
			SetUp();
			var textReader = new StreamReader("test.test");
			var config = FastaFile.Open(textReader);

			var fastaFileReader = new FastaFileReader(config.kMerSize, config.nCharsInFile, textReader, 1000, 2);

			ulong[] kMerBuffer = new ulong[1024];

			int returned = 0;
			ulong sum = 0;
			while (true)
			{
				var returnedNow = fastaFileReader.FillBuffer(kMerBuffer);
				if (returnedNow == 0) break;
				returned += returnedNow;
				foreach (var item in kMerBuffer)
				{
					sum += item;
				}
			}

			if (returned != 1_000_000 - 30) throw new Exception($"Returned is not equal to 100_000_000 {returned}");
			fastaFileReader.Dispose();
			return (int)sum;
		}
		public void Cleanup()
		{
			File.Delete("test.test");
		}
	}

}