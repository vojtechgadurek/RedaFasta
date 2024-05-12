using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace RedaFastaTest
{
	public static class Utils
	{

		public static char TranslateUlongToChar(ulong symbol)
		{
			switch (symbol)
			{
				case 0: return 'A';
				case 1: return 'C';
				case 2: return 'G';
				case 3: return 'T';
				default: throw new ArgumentException("Invalid kMer");
			}
		}

		public static ulong GetNThValue(ulong kmer, int size, int n)
		{
			ulong mask = 0b11UL;
			kmer >>>= ((size - n - 1) * 2 + 2);
			return kmer & mask;
		}

		public static string TranslateUlongToString(ulong kMer, int size)
		{
			char[] chars = new char[size];
			for (int i = 0; i < size; i++)
			{
				chars[i] = TranslateUlongToChar(GetNThValue(kMer, size, i));
			}
			return new string(chars);

		}
	}
}
