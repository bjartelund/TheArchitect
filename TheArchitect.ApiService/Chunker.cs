namespace TheArchitect.ApiService;

public static class Chunker
{

        private const int ChunkSize = 5000;
        private const int Overlap = 500;

        public static IEnumerable<string> Chunk(string text)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            const int step = ChunkSize - Overlap;

            for (var start = 0; start < text.Length; start += step)
            {
                var length = Math.Min(ChunkSize, text.Length - start);
                yield return text.Substring(start, length);
            }
        }
}