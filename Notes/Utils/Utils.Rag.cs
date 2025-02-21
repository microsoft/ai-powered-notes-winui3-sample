using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Notes.AI;

namespace Notes
{
    internal partial class Utils
    {
        public static async IAsyncEnumerable<string> Rag(string question, [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (App.ChatClient == null)
            {
                yield return string.Empty;
            }
            else
            {
                var searchResults = await SearchAsync(question, top: 2);

                var content = string.Join(" ", searchResults.Select(c => c.Content).ToList());

                var systemMessage = "You are a helpful assistant answering questions about this content";

                await foreach (var token in App.ChatClient.InferStreaming($"{systemMessage}: {content}", question, ct))
                {
                    yield return token;
                }
            }
        }
    }
}
