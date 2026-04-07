using System.Diagnostics;
using System.Text;

namespace _1brc_cs;

class Program
{
    struct MeasurementStats
    {
        public int Min = 0;
        public int Max = 0;
        public int Sum = 0;
        public int NumEntries = 0;

        public MeasurementStats()
        { }
    }

    static void Main(string[] args)
    {
        Console.WriteLine("=== 1BRC ===");

        var sw = new Stopwatch();
        sw.Start();

        // // Read and Dump Weather Stations
        // var cities = ReadWeatherStations();
        // foreach (var city in cities)
        //     Console.WriteLine(city);

        // Read and Output Measurements
        var measurements = ReadMeasurements();
        foreach (var stat in measurements)
        {
            // Emits the results on stdout: sorted alphabetically by station name, values per station, rounded to one fractional digit
            var avg = (float)stat.Value.Sum / stat.Value.NumEntries / 10f;
            Console.WriteLine($"Station: {stat.Key,-20} = Min({stat.Value.Min,4}), Max({stat.Value.Max,4}), Avg({avg,5:F1}) <-- {stat.Value.Sum/10f,5:F1} / {stat.Value.NumEntries}");

        }

        sw.Stop();
        Console.WriteLine($"Seconds Elapsed: {sw.Elapsed.TotalSeconds}");

        // DebugStats(measurements);
    }

    static void DebugStats(SortedDictionary<string,MeasurementStats> stats)
    {
        foreach (var stat in stats)
        {
            var avg = stat.Value.Sum / stat.Value.NumEntries;
            if (stat.Value.Min != stat.Value.Max || stat.Value.NumEntries > 1)
                Console.WriteLine($"Station: {stat.Key,-20} = Min({stat.Value.Min,3}), Max({stat.Value.Max,3}), Avg({avg,3}) <-- {stat.Value.Sum,4} / {stat.Value.NumEntries}");
        }
        Console.WriteLine($"Measurements Count: {stats.Count} (a.k.a Stations)");
    }

    /// <summary>
    /// Reads through the measurements chunks to calculate the Sum, Min, Max
    /// </summary>
    /// <returns></returns>
    static SortedDictionary<string, MeasurementStats> ReadMeasurements()
    {
        var sw = new Stopwatch();
        var totalEntries = 0;
        var prevCount = totalEntries;

        var measurements = new SortedDictionary<string, MeasurementStats>();
        foreach (var chunk in ReadMeasurementChunks())
        {
            sw.Restart();
            var entries = 0;
            
            foreach (var measurement in chunk.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var (stationName, value) = StationParseValue(measurement);
                if (measurements.TryGetValue(stationName, out var stats))
                {
                    stats.Sum += value;
                    stats.Min = Math.Min(stats.Min, value);
                    stats.Max = Math.Max(stats.Max, value);
                    stats.NumEntries += 1;
                    measurements[stationName] = stats;
                }
                else
                {
                    measurements[stationName] = new MeasurementStats
                    {
                        Sum = value,
                        Min = value,
                        Max = value,
                        NumEntries = 1
                    };

                }
                entries++;
            }

            sw.Stop();
            totalEntries += entries;
            if (totalEntries > prevCount + 1_000_000)
            {
                Console.WriteLine($"Elapsed: {sw.Elapsed.TotalMicroseconds} micro-secs for {entries} entries, total_entries: {totalEntries}");
                prevCount = totalEntries;
            }
        }

        return measurements;
    }

    /// <summary>
    /// Reads chunks of text from the file, returned chunks
    /// always end as a complete string ending on a newline
    /// </summary>
    /// <param name="path"></param>
    /// <param name="chunkSize"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    static IEnumerable<string> ReadMeasurementChunks(string path = "../measurements.txt", int chunkSize = 256, int limit = 1_000_000)
    {
        var buf = new byte[chunkSize];
        var chunk = chunkSize;
        var offset = 0;
        int bytesRead;
        var fileBytes = 0;

        using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        while ((bytesRead = fs.Read(buf, offset, chunk)) > 0 && (limit == 0 || fileBytes < limit))
        {
            var totalBytes = offset + bytesRead;
            fileBytes += totalBytes;
            
            var lastNewlinePos = LastIndexOf(buf, totalBytes) +1;
            var slice = buf[..lastNewlinePos];
            var rest = buf[lastNewlinePos..];
            rest.CopyTo(buf, 0);

            var decodedString = Encoding.UTF8.GetString(slice);

            yield return decodedString;

            offset = rest.Length;
            chunk = chunkSize - rest.Length;
        }
    }

    /// <summary>
    /// Convenience function to extract the name and value from the string
    /// </summary>
    /// <param name="kvp"></param>
    /// <returns></returns>
    static (string stationName, int value) StationParseValue(string kvp)
    {
        var parts = kvp.Split(";");
        var stationName = parts[0];
        int value = (int)(float.Parse(parts[1]) * 10);  // Convert 1dp float to int
        return (stationName, value);
    }

    static int LastIndexOf(byte[] buf, int totalBytes, char token = '\n')
    {
        for (int i = totalBytes-1; i >= 0; i--)
            if (buf[i] == (byte)token) return i;

        return 0;
    }

    /// <summary>
    /// Reads chunks of weather stations from generator and initialises the dictionary
    /// </summary>
    /// <returns></returns>
    static Dictionary<string, int> ReadWeatherStations()
    {
        var cities = new Dictionary<string,int>();

        foreach (var chunk in ReadWeatherStationChunks())
        {
            foreach (var city in chunk.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var (stationName, value) = StationParseValue(city);
                cities[stationName] = value;
            }
        }

        return cities;
    }

    /// <summary>
    /// Reads chunks of text from the file, returned chunks
    /// always end as a complete string ending on a newline
    /// Skips any comment lines at start of file
    /// </summary>
    /// <param name="path"></param>
    /// <param name="chunkSize"></param>
    /// <returns></returns>
    static IEnumerable<string> ReadWeatherStationChunks(string path = "../data/weather_stations.csv", int chunkSize = 1024)
    {
        var buf = new byte[chunkSize];
        var chunk = chunkSize;
        var offset = 0;
        int bytesRead;

        using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, chunkSize, false);
        while ((bytesRead = fs.Read(buf, offset, chunk)) > 0)
        {
            var skip = 0;
            Span<byte> span;
            while ((span = buf.AsSpan(skip))[0] == (byte)'#')
            {
                skip += span.IndexOf((byte)'\n') +1;
                // var text = Encoding.UTF8.GetString(span[..skip]);
                // Console.WriteLine($"Skipping: {skip} --> {text}");
            }

            var totalBytesRead = bytesRead + offset;
            var lastNewLinePos = LastIndexOf(buf, totalBytesRead) +1;
            var slice = buf[skip..lastNewLinePos];
            var rest = buf[lastNewLinePos..totalBytesRead];
            rest.CopyTo(buf, 0);
            
            var decodedLine = Encoding.UTF8.GetString(slice);

            yield return decodedLine;

            offset = rest.Length;
            chunk = chunkSize - rest.Length;
        }
    }
}
