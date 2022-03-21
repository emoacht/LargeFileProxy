using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace LargeFileProxy
{
	public class StreamReaderPlus : StreamReader
	{
		public StreamReaderPlus(Stream stream) : base(stream)
		{ }

		public StreamReaderPlus(Stream stream, Encoding encoding) : base(stream, encoding)
		{ }

		public IEnumerable<(string value, long offset)> ReadLinesPositions()
		{
			long offset = BaseStream.Position;

			while (true)
			{
				(string? value, int count) = ReadLine();
				if (value is null)
					break;

				yield return (value, offset);
				offset += count;
			}

			Debug.Assert(offset == BaseStream.Position, "Offset doesn't match.");

			(string? value, int count) ReadLine()
			{
				StringBuilder buffer = new();

				while (true)
				{
					int ch = Read();
					if (ch is -1)
						break;

					if (ch is '\r' or '\n')
					{
						int count = 1;

						if (ch is '\r' && Peek() is '\n')
						{
							Read();
							count++;
						}

						string value = buffer.ToString();
						count += CurrentEncoding.GetByteCount(value);
						return (value, count);
					}

					buffer.Append((char)ch);
				}

				if (buffer.Length > 0)
				{
					string value = buffer.ToString();
					int count = CurrentEncoding.GetByteCount(value);
					return (value, count);
				}

				return (null, 0);
			}
		}
	}
}