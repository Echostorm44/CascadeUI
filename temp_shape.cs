using System;
using HarfBuzzSharp;
class Program {
    static void Main(string[] args) {
        string path = args[0];
        string text = args[1];
        using var blob = Blob.FromFile(path);
        using var face = new Face(blob, 0);
        using var font = new Font(face);
        font.SetScale(40*64, 40*64);
        using var buffer = new Buffer();
        buffer.AddUtf16(text);
        buffer.GuessSegmentProperties();
        font.Shape(buffer, Array.Empty<Feature>());
        var infos = buffer.GlyphInfos;
        var pos = buffer.GlyphPositions;
        Console.WriteLine($"UnitsPerEm={face.UnitsPerEm}, GlyphCount={infos.Length}");
        for (int i = 0; i < infos.Length; i++) {
            Console.WriteLine($"  {i}: gid={infos[i].Codepoint}, cluster={infos[i].Cluster}, adv={pos[i].XAdvance/64f:F2}");
        }
    }
}