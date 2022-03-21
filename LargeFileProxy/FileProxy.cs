using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LargeFileProxy
{
	public class FileProxy : IDisposable
	{
		private class LineItem
		{
			public long Offset { get; }
			public int Length { get; }

			public LineItem(long offset, int length) => (Offset, Length) = (offset, length);
		}

		private readonly FileStream _stream;
		private readonly StreamReaderPlus _reader;
		private readonly StreamWriter _writer;

		public FileProxy(string filePath) : this(filePath, new UTF8Encoding(false) /* UTF-8 w/o BOM */)
		{ }

		public FileProxy(string filePath, Encoding encoding)
		{
			_stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
			_reader = new StreamReaderPlus(_stream, encoding);
			_writer = new StreamWriter(_stream, encoding);
		}

		#region IDisposable

		private bool _disposed = false;

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (_disposed)
				return;

			if (disposing)
			{
				_reader.Dispose();
				_writer.Dispose();
				_stream.Dispose();
			}

			_disposed = true;
		}

		#endregion

		private readonly Dictionary<int, LineItem[]> _lines = new();

		public bool IsInitialized { get; private set; }

		public Task InitializeAsync()
		{
			if (IsInitialized)
				return Task.CompletedTask;

			return Task.Run(() =>
			{
				foreach ((string content, long offset) in _reader.ReadLinesPositions())
				{
					Debug.WriteLine($"Initialize: {content} | {content.Length} | {offset}");

					var key = content.GetHashCode();
					LineItem item = new(offset, content.Length);

					_lines[key] = _lines.TryGetValue(key, out var items)
						? items.Append(item).ToArray()
						: new[] { item };
				}

				IsInitialized = true;
			});
		}

		private void ThrowIfNotInitialized()
		{
			if (!IsInitialized)
				throw new InvalidOperationException("Initialization is required.");
		}

		public async Task AddDistinctAsync(string content)
		{
			ThrowIfNotInitialized();

			content = content.TrimEnd(new[] { '\r', '\n' });
			var key = content.GetHashCode();

			if (_lines.TryGetValue(key, out var items))
			{
				foreach (var item in items)
				{
					if (await ReadAsync(item) == content)
					{
						Debug.WriteLine($"NOT Added: {content}");
						return;
					}
				}
			}

			{
				LineItem item = await WriteLineAsync(content);
				_lines[key] = (items is not null)
					? items.Append(item).ToArray()
					: new[] { item };

				Debug.WriteLine($"Added: {content} | {item.Length} | {item.Offset}");
			}
		}

		public async Task<string[]> RetrieveAsync()
		{
			ThrowIfNotInitialized();

			List<string> buffer = new();

			foreach (var item in _lines.Values.SelectMany(x => x))
			{
				buffer.Add(await ReadAsync(item));
				Debug.WriteLine($"Retrieve: {buffer.Last()} | {item.Length} | {item.Offset}");
			}

			return buffer.ToArray();
		}

		private async Task<string> ReadAsync(LineItem item)
		{
			_stream.Seek(item.Offset, SeekOrigin.Begin);

			char[] buffer = new char[item.Length];
			await _reader.ReadAsync(buffer, 0, item.Length);
			_reader.DiscardBufferedData();
			return new string(buffer);
		}

		private async Task<LineItem> WriteLineAsync(string content)
		{
			_stream.Seek(0, SeekOrigin.End);

			long offset = _stream.Position;
			await _writer.WriteAsync(content + Environment.NewLine);
			await _writer.FlushAsync();
			return new LineItem(offset, content.Length);
		}
	}
}