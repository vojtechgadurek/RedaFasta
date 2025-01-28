using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RedaFasta
{
    public interface IFastaFileReader
    {
        void RecycleBuffer(FastaFileReader.Buffer buffer);
        FastaFileReader.Buffer? BorrowBuffer();
        int FillBuffer(ulong[] kMers);
        void Dispose();
    }
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

            int kMerSize;
            long nCharsInFile;


            var dic = ParseHeaderLine(firstLine);
            if (!dic.ContainsKey("k")) throw new ArgumentException("Header does not contain k parameter");
            else kMerSize = int.Parse(dic["k"]);
            if (!dic.ContainsKey("l"))
            {
                if (textReader is StreamReader sr)
                {
                    nCharsInFile = sr.BaseStream.Length - sr.BaseStream.Position;
                }
                else
                {
                    throw new ArgumentException("Header does not contain l parameter");
                }
            }
            else
            {
                nCharsInFile = long.Parse(dic["l"]);
            }

            return (textReader, kMerSize, nCharsInFile);
        }

        /// <summary>
        /// Parses the header line of a Fasta file and returns a dictionary of the parsed _data.
        /// </summary>
        /// <param name="header">The header line to parse.</param>
        /// <returns>A dictionary where the key is the parameter name and the value is the parameter value.</returns>
        /// <exception cref="ArgumentException">Thrown when the header is not in the correct format.</exception>
        /// <remarks>
        /// This method splits the header line by spaces and then by equals sign to extract the parameter name and value.
        /// It skips the first item because It is assumed to be the identifier of the sequence.
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
        /// Parses the first line of _data in a Fasta file and returns a tuple containing the kMerLength and nCharsInFile.
        /// </summary>
        /// <param name="header">The first line of _data to parse.</param>
        /// <returns>A tuple where the first item is the kMerLength (k) and the second item is the number of characters in the file (l).</returns>
        /// <exception cref="ArgumentException">Thrown when the header does not contain the required parameters (k and l).</exception>
        /// <remarks>
        /// This method uses the ParseHeaderLine method to parse the header line and extract the parameters.
        /// It then checks if the required parameters (k and l) are present in the dictionary returned by ParseHeaderLine.
        /// If any of the required parameters are missing, It throws an ArgumentException.
        /// </remarks>
        public static (int? size, long? nCharsInFile) ParseFirstLineData(string header)
        {

            var dic = ParseHeaderLine(header);
            if (!dic.ContainsKey("k")) throw new ArgumentException("Header does not contain k parameter");
            if (!dic.ContainsKey("l"))
                throw new ArgumentException("Header does not contain l parameter");

            return (int.Parse(dic["k"]), long.Parse(dic["l"]));
        }

    }

    public class FastaFileReader : IFastaFileReader
    {
        readonly ulong _sizeMask;
        public readonly int KMerLength;
        long _charsLeft;
        public long CharsLeft { get => _charsLeft; }
        readonly int _lastCharsLength;
        long _length;
        readonly char[] _lastChars;

        char[] _charBuffer;
        Task<int>? _numberCharsInBuffer = null;


        readonly int _bufferSize;
        readonly int _bufferCount;
        bool _finished = false;
        bool _inited = false;
        int _nThreadsUsed;
        int _nThreadsMax;
        BufferFiller[] _workers;


        public class BufferWorker
        {
            Thread _thread;
            Buffer _buffer;
            char[] _data;
            int _nChars;
            int _start;
            FastaFileReader _parent;
            bool _finished;
            bool Running => _thread.ThreadState == ThreadState.Running;
            public BufferWorker()
            {
                _thread = new Thread(TranslateInner);
            }

            void TranslateInner()
            {
                while (true)
                {
                    if (_finished == true) return;
                    _buffer.Used = 0;
                    _buffer.Size = _parent.Translate(_buffer.Data, _data, _start, _nChars);
                    Thread.Sleep(Timeout.Infinite);
                }
            }
            public bool TryTranslate(Buffer buffer, char[] data, int start, int nChars)
            {
                if (_thread.ThreadState == ThreadState.Running) return false;
                _buffer = buffer;
                _data = data;
                _start = start;
                _nChars = nChars;

                _thread.Start();
                return true;
            }

            public void Close()
            {
                _finished = true;
                _thread.Start();
            }
        }
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

        //This is _buffer method! 
        static void Fill(Buffer it, char[] charBuffer, int start, int numberOfItems, FastaFileReader parent)
        {
            it.Size = parent.Translate(it.Data, charBuffer, start, numberOfItems);
        }

        class BufferFiller
        {

            public Buffer? It;
            public char[]? CharBuffer;
            public int Start;
            public int NumberOfItems;
            public FastaFileReader? Parent;
            public Task? Task;
            public BufferFiller()
            {
            }

            public void Fill()
            {
                Task = Task.Run(() =>
                {
                    FastaFileReader.Fill(It, CharBuffer, Start, NumberOfItems, Parent);
                });
            }

            public void Clear()
            {
                It = null;
                CharBuffer = null;
                Parent = null;
                Task = null;
            }

        }
        List<Buffer> _freeBuffers = new List<Buffer>();
        List<Buffer> _usedBuffers = new List<Buffer>();

        TextReader _textReader;
        public FastaFileReader(int kMerLength, long length, TextReader textReader, int bufferSize = 1024 * 64, int bufferCount = 16, bool init = true)
        {
            if (kMerLength < 1) throw new ArgumentException("Size of kMer is too small, min is 1");
            if (kMerLength > 31) throw new ArgumentException($"Size of kMer is too big, max is 31, currently {kMerLength}");
            if (bufferSize < 1) throw new ArgumentException("Buffer kMerLength is too small, min is 1");
            if (kMerLength - 1 > bufferSize) bufferSize = kMerLength - 1;

            if (bufferCount < 1) throw new ArgumentException("Buffer count is too small, min is 1");

            KMerLength = kMerLength;
            _textReader = textReader;
            _charsLeft = length;
            _length = length;
            _bufferSize = bufferSize;
            _bufferCount = bufferCount;
            _nThreadsMax = bufferCount;
            _nThreadsUsed = 0;
            _workers = new BufferFiller[bufferCount];
            for (int i = 0; i < _nThreadsMax; i++)
            {
                _workers[i] = new BufferFiller();
            }

            _sizeMask = (1UL << (kMerLength * 2)) - 1;
            _lastChars = new char[kMerLength - 1];
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


            for (int i = 0; i < instancesToRun; i++)
            {
                var thread = _workers[i];
                thread.It = workedOn[i];
                thread.CharBuffer = _charBuffer;
                thread.Start = i * _bufferSize;
                thread.Parent = this;

                if (i != instancesToRun - 1 || read % _bufferSize == 0)
                {

                    thread.NumberOfItems = _bufferSize + _lastCharsLength;
                }
                else
                {
                    thread.NumberOfItems = read % _bufferSize + _lastCharsLength;
                }
                thread.Fill();
            };

            for (int i = 0; i < instancesToRun; i++)
            {
                _workers[i].Task.Wait();
                _workers[i].Clear();
            }


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

            //Fill first KMerLength bucket with overfill from last
            for (int i = 0; i < _lastCharsLength; i++)
            {
                buffer[i] = _lastChars[i];
            }

            int toRead = buffer.Length - _lastCharsLength;
            if (toRead > _charsLeft) toRead = (int)_charsLeft;

            int read = _textReader.Read(buffer, _lastCharsLength, toRead);

            _charsLeft -= read;
            if (read == 0) _finished = true;

            //Save last KMerLength chars
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
                return header | (kMer << 2);
            }

            (ulong kMer, ulong complement) PushSymbolIn(ulong kMer, ulong complement, char symbol)
            {

                int val = (((int)symbol) >> 1) & 0b11;

                kMer <<= 2;
                kMer |= permutation[val];


                complement >>>= 2;
                complement |= (~permutation[val] << (KMerLength * 2 - 2));

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

                if (IsUpper(chars[index - _lastCharsLength]))
                {
                    ulong kMerSelected = kMer < complement ? kMer : complement;

                    kMers[kMerCreated] = ApplyHeader(kMerSelected);
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
