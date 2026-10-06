using System;
using System.IO;
using System.Threading.Tasks;

namespace Airside.Presentation
{
    /// <summary>CPU encoding and filesystem writes never run on the render thread.</summary>
    public static class BackgroundCaptureWrite
    {
        public static Task Start(string path, Func<byte[]> encode)
        {
            if (encode == null) throw new ArgumentNullException(nameof(encode));
            return Task.Run(() => File.WriteAllBytes(path, encode()));
        }
    }
}
