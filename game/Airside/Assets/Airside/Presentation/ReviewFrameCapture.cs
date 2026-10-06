using System;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Airside.Presentation
{
    /// <summary>Finished-frame QA capture without a synchronous GPU fence or disk write.</summary>
    public static class ReviewFrameCapture
    {
        private const double TimeoutSeconds = 15;

        public static IEnumerator Capture(string path, Action<bool> completed, Action submitted = null)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            if (!SystemInfo.supportsAsyncGPUReadback)
            {
                Debug.LogError("[Airside capture] asynchronous GPU readback is unsupported; " + path);
                completed(false);
                yield break;
            }

            var timer = Stopwatch.StartNew();
            var width = Screen.width;
            var height = Screen.height;
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            Task write = null;
            string failure = null;
            var abandoned = false;
            Debug.Log("[Airside capture] request " + path);
            try
            {
                target.Create();
                submitted?.Invoke();
                ScreenCapture.CaptureScreenshotIntoRenderTexture(target);
                AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, request =>
                {
                    try
                    {
                        if (abandoned) return;
                        if (request.hasError)
                        {
                            failure = "GPU readback failed";
                            return;
                        }
                        // Copy before Unity releases request-owned memory. The worker gets only
                        // managed pixels, never a Texture/RenderTexture or other Unity object.
                        var pixels = request.GetData<byte>().ToArray();
                        Debug.Log($"[Airside capture] readback {timer.Elapsed.TotalMilliseconds:0} ms {path}");
                        write = BackgroundCaptureWrite.Start(path, () =>
                            ImageConversion.EncodeArrayToPNG(pixels, GraphicsFormat.R8G8B8A8_UNorm,
                                (uint)width, (uint)height));
                    }
                    catch (Exception e) { failure = e.Message; }
                    finally
                    {
                        // An outstanding GPU request owns its target until this callback, even
                        // if the coroutine timed out. Never release it beneath an active request.
                        target.Release();
                        UnityEngine.Object.Destroy(target);
                    }
                });
            }
            catch (Exception e)
            {
                target.Release();
                UnityEngine.Object.Destroy(target);
                failure = e.Message;
            }

            while (failure == null && (write == null || !write.IsCompleted)
                && timer.Elapsed.TotalSeconds < TimeoutSeconds)
                yield return null;

            if (failure == null && (write == null || !write.IsCompleted))
            {
                abandoned = true;
                failure = "capture exceeded 15 seconds";
            }
            if (write != null && write.IsFaulted)
                failure = write.Exception.GetBaseException().Message;
            if (failure != null)
            {
                Debug.LogError($"[Airside capture] {failure}: {path}");
                completed(false);
                yield break;
            }
            Debug.Log($"[Airside capture] written {timer.Elapsed.TotalMilliseconds:0} ms {path}");
            completed(true);
        }
    }
}
