using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

using Serializer = Newtonsoft.Json.JsonSerializer;

namespace Unleash.Serialization
{
	public class JsonSerializer : IJsonSerializer
	{
		private readonly Serializer serializer = Serializer.Create();
		
		public T Deserialize<T>(Stream stream)
		{
			using (var streamReader = new StreamReader(stream, Encoding.UTF8))
			{
				JsonTextReader textReader = new JsonTextReader(streamReader);

				try
				{
					return serializer.Deserialize<T>(textReader);
				}
				finally
				{
					(textReader as IDisposable)?.Dispose();
				}
			}
		}

		public void Serialize<T>(Stream stream, T instance)
		{
			// Default
			const int bufferSize = 1024 * 4;

			// Client code needs to dispose this.
			const bool leaveOpen = true;

			using (var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize, leaveOpen: leaveOpen))
			{
				JsonTextWriter jsonWriter = new JsonTextWriter(writer);

				try
				{
					serializer.Serialize(jsonWriter, instance);

					jsonWriter.Flush();
					stream.Position = 0;
				}
				finally
				{
					(jsonWriter as IDisposable)?.Dispose();
				}
			}
		}
	}
}