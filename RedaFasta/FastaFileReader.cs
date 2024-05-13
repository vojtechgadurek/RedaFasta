using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RedaFasta
{
	public static class FastaFile
	{
		/// <summary>
		/// Opens a Fasta file and returns a tuple containing the TextReader, kMerSize, and nCharsInFile.
		/// </summary>
		/// <param name="textReader">The TextReader to read the Fasta file.</param>
		/// <returns>A tuple containing the TextReader, kMerSize, and nCharsInFile.</returns>
		public static (TextReader textReader, int kMerSize, long nCharsInFile) Open(TextReader textReader)
		{
			var firstLine = textReader.ReadLine();
			if (firstLine is null) throw new InvalidOperationException($"File is empty");
			var (size, nCharsInFile) = ParseFirstLineData(firstLine);
			return (textReader, size, nCharsInFile);
		}

		/// <summary>
		/// Parses the header line of a Fasta file and returns a dictionary of the parsed data.
		/// </summary>
		/// <param name="header">The header line to parse.</param>
		/// <returns>A dictionary where the key is the parameter name and the value is the parameter value.</returns>
		/// <exception cref="ArgumentException">Thrown when the header is not in the correct format.</exception>
		/// <remarks>
		/// This method splits the header line by spaces and then by equals sign to extract the parameter name and value.
		/// It skips the first item because it is assumed to be the identifier of the sequence.
		/// </remarks>
		public static IDictionary<string, string> ParseHeaderLine(string header)
		{
			var splited = header.Split(' ');
			if (splited is null) throw new ArgumentException("Header is not in correct format");

			var dictionary = new Dictionary<string, string>();
			foreach (var item in splited.Skip(1))
			{
				var splitedItem = item.Split('=');
				if (splitedItem.Length != 2)
					throw new ArgumentException($"Header is not in correct format {splitedItem.Aggregate(new StringBuilder(), (a, b) => { a.Append(b); return a; })}");
				dictionary.Add(splitedItem[0], splitedItem[1]);
			}

			return dictionary;
		}

		/// <summary>
		/// Parses the first line of data in a Fasta file and returns a tuple containing the size and nCharsInFile.
		/// </summary>
		/// <param name="header">The first line of data to parse.</param>
		/// <returns>A tuple where the first item is the size (k) and the second item is the number of characters in the file (l).</returns>
		/// <exception cref="ArgumentException">Thrown when the header does not contain the required parameters (k and l).</exception>
		/// <remarks>
		/// This method uses the ParseHeaderLine method to parse the header line and extract the parameters.
		/// It then checks if the required parameters (k and l) are present in the dictionary returned by ParseHeaderLine.
		/// If any of the required parameters are missing, it throws an ArgumentException.
		/// </remarks>
		public static (int size, long nCharsInFile) ParseFirstLineData(string header)
		{

			var dic = ParseHeaderLine(header);
			if (!dic.ContainsKey("k")) throw new ArgumentException("Header does not contain k parameter");
			if (!dic.ContainsKey("l")) throw new ArgumentException("Header does not contain l parameter");

			return (int.Parse(dic["k"]), long.Parse(dic["l"]));
		}

	}

	public class FastaFileReader
	{
		readonly ulong _sizeMask;
		readonly int _size;
		long _charsLeft;
		readonly int _lastCharsLength;
		long _length;
		readonly char[] _lastChars;

		char[] _charBuffer;
		Task<int>? _numberCharsInBuffer = null;


		readonly int _bufferSize;
		readonly int _bufferCount;
		bool _finished = false;
		bool _inited = false;


		public class Buffer
		{
			public ulong[] Data;
			public int Size = 0;
			public int Used = 0;

			public Buffer(ulong[] data)
			{
				Data = data;
			}


		}

		//This is buffer method! 
		void Fill(Buffer it, char[] charBuffer, int start, int numberOfItems, FastaFileReader parent)
		{
			it.Size = parent.Translate(it.Data, charBuffer, start, numberOfItems);
		}

		List<Buffer> _freeBuffers = new List<Buffer>();
		List<Buffer> _usedBuffers = new List<Buffer>();

		TextReader _textReader;
		public FastaFileReader(int size, long length, TextReader textReader, int bufferSize = 1024 * 1024, int bufferCount = 8, bool init = true)
		{
			if (size < 1) throw new ArgumentException("Size of kMer is too small, min is 1");
			if (size > 31) throw new ArgumentException($"Size of kMer is too big, max is 31, currently {size}");
			if (bufferSize < 1) throw new ArgumentException("Buffer size is too small, min is 1");
			if (size - 1 > bufferSize) bufferSize = size - 1;

			if (bufferCount < 1) throw new ArgumentException("Buffer count is too small, min is 1");


			_size = size;
			_textReader = textReader;
			_charsLeft = length;
			_length = length;
			_bufferSize = bufferSize;
			_bufferCount = bufferCount;

			_sizeMask = (1UL << (size * 2 + 2)) - 1 - 0b11;
			_lastChars = new char[size - 1];
			_lastCharsLength = _lastChars.Length;


			_charBuffer = new char[_bufferSize * _bufferCount + _lastCharsLength];


			for (int i = 0; i < _bufferCount; i++)
			{
				_freeBuffers.Add(new Buffer(new ulong[_bufferSize]));
			}

			if (init == true) Init();
		}


		public void Init()
		{
			if (_inited == false) _charsLeft -= _textReader.Read(_lastChars, 0, _charsLeft > _lastChars.LongLength ? _lastCharsLength : (int)_charsLeft);
			_inited = true;
		}

		void FillKMerBuffers()
		{
			while (_freeBuffers.Count < _bufferCount)
			{
				_freeBuffers.Add(new Buffer(new ulong[_bufferSize]));
			}

			if (_numberCharsInBuffer is null)
			{
				FillCharBufferAsync();
			}

			_numberCharsInBuffer!.Wait();

			int read = _numberCharsInBuffer.Result;

			_numberCharsInBuffer = null;

			int instancesToRun = read / _bufferSize;
			if (read % _bufferSize != 0)
			{
				instancesToRun++;
			}

			List<Buffer> workedOn = new();
			for (int i = 0; i < instancesToRun; i++)
			{
				Buffer buffer = _freeBuffers[_freeBuffers.Count - 1];
				_freeBuffers.RemoveAt(_freeBuffers.Count - 1);
				workedOn.Add(buffer);
			}

			Parallel.For(0, instancesToRun,
				(i) =>
				{
					if (i != instancesToRun - 1)
					{
						Fill(workedOn[i], _charBuffer, i * _bufferSize, _bufferSize + _lastCharsLength, this);

					}
					else if (read % _bufferSize == 0)
					{
						Fill(workedOn[i], _charBuffer, i * _bufferSize, _bufferSize + _lastCharsLength, this);

					}
					else
					{
						Fill(workedOn[i], _charBuffer, i * _bufferSize, read % _bufferSize + _lastCharsLength, this);
					}
				});

			foreach (var buffer in workedOn) _usedBuffers.Add(buffer);

			FillCharBufferAsync();
		}

		void FillCharBufferAsync()
		{
			if (_numberCharsInBuffer is null)
			{
				_numberCharsInBuffer = Task.Run(() =>
			  {
				  return FillCharBuffer(_charBuffer);
			  });
			}
		}

		int FillCharBuffer(char[] buffer)
		{

			//Fill first _size bucket with overfill from last
			for (int i = 0; i < _lastCharsLength; i++)
			{
				buffer[i] = _lastChars[i];
			}

			int toRead = buffer.Length - _lastCharsLength;
			if (toRead > _charsLeft) toRead = (int)_charsLeft;

			int read = _textReader.Read(buffer, _lastCharsLength, toRead);

			_charsLeft -= read;
			if (read == 0) _finished = true;

			//Save last _size chars
			for (int i = 0; i < _lastCharsLength && read > i; i++)
			{
				_lastChars[i] = buffer[read + i];
			}

			return read;
		}

		public void RecycleBuffer(Buffer buffer)
		{
			buffer.Size = 0;
			buffer.Used = 0;
			_freeBuffers.Add(buffer);
		}

		public Buffer? BorrowBuffer()
		{
			if (_usedBuffers.Count == 0)
			{
				FillKMerBuffers();
			}

			if (_finished == true && _usedBuffers.Count == 0) return null;


			Buffer buffer = _usedBuffers[_usedBuffers.Count - 1];
			_usedBuffers.RemoveAt(_usedBuffers.Count - 1);
			return buffer;



		}
		public int FillBuffer(ulong[] kMers)
		{

			int returned = 0;

			while (returned < kMers.Length)
			{
				if (_usedBuffers.Count == 0)
				{
					FillKMerBuffers();
				}

				if (_finished == true && _usedBuffers.Count == 0) break;

				Buffer buffer = _usedBuffers[_usedBuffers.Count - 1];
				_usedBuffers.RemoveAt(_usedBuffers.Count - 1);

				int itemsToCopy = (kMers.Length - returned) < (buffer.Size - buffer.Used) ? kMers.Length - returned : buffer.Size - buffer.Used;
				Array.Copy(buffer.Data, buffer.Used, kMers, returned, itemsToCopy);
				returned += itemsToCopy;
				buffer.Used += itemsToCopy;
				if (buffer.Used == buffer.Size)
				{
					buffer.Used = 0;
					buffer.Size = 0;
					_freeBuffers.Add(buffer);
				}
				else
				{
					_usedBuffers.Add(buffer);
				}
			}
			return returned;
		}

		public int Translate(ulong[] kMers, char[] chars, int start, int numberCharToUse)
		{
			//    baserepr  shift  permutation
			// 	A 01000001 -> 00 -> 00
			//  C 01000011 -> 01 -> 01
			//  G 01000111 -> 11 -> 10
			//  T 01010100 -> 10 -> 11


			ulong[] permutation = { 0, 1, 3, 2 };
			ulong header = 0b11;

			ulong ApplyHeader(ulong kMer)
			{
				return header | kMer;
			}

			(ulong kMer, ulong complement) PushSymbolIn(ulong kMer, ulong complement, char symbol)
			{
				int val = (((int)symbol) >> 1) & 0b11;
				kMer |= permutation[val];
				kMer <<= 2;

				complement >>>= 2;
				complement |= (~permutation[val] << (_size * 2));

				kMer &= _sizeMask;
				complement &= _sizeMask;

				return (kMer, complement);
			}

			bool IsUpper(char c)
			{
				int val = (int)c;
				const int mask = 0b0010_0000;
				val = (val & mask);
				if (mask == val) return false;
				return true;

			}

			if (kMers.Length < numberCharToUse - _lastCharsLength)
			{
				throw new InvalidOperationException($"KMer array is too small, {kMers.Length} is less than {numberCharToUse}");
			}

			if (chars.Length < numberCharToUse - _lastCharsLength)
			{
				throw new InvalidOperationException($"Chars array is too small, {chars.Length} is less than {numberCharToUse}");
			}

			int index = start;

			//Build kMer and complement
			ulong kMer = 0;
			ulong complement = 0;

			for (int i = 0; i < _lastCharsLength; i++)
			{

				(kMer, complement) = PushSymbolIn(kMer, complement, chars[index]);
				index++;

			}

			int kMerCreated = 0;
			while (index - start < numberCharToUse)
			{

				(kMer, complement) = PushSymbolIn(kMer, complement, chars[index]);

				ulong kMerValue = ApplyHeader(kMer);
				ulong complementValue = ApplyHeader(complement);

				if (IsUpper(chars[index - _lastCharsLength]))
				{
					kMers[kMerCreated] = kMerValue < complementValue ? kMerValue : complementValue;
					kMerCreated++;
				};

				index++;
			}

			return kMerCreated;
		}

		public void Dispose()
		{
			_textReader.Dispose();
		}
	}
}
