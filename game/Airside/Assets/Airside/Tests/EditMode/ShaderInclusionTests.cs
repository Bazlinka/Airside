using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    /// <summary>
    /// Code that calls <c>Shader.Find</c> only works in a player build if the shader is in Always
    /// Included Shaders; nothing else references it, so the build strips it and Find returns null.
    /// The propeller blur, fan discs and painted stand labels all need URP Unlit: without it the prop
    /// disc fell back to a flat translucent glass square (seen in the built game).
    /// </summary>
    public sealed class ShaderInclusionTests
    {
        private const string UrpUnlitGuid = "650dd9526735d5b46b79224bc6e94025";

        [Test]
        public void UrpUnlit_IsAlwaysIncludedInBuilds()
        {
            var path = Path.Combine(Application.dataPath, "..", "ProjectSettings", "GraphicsSettings.asset");
            Assert.That(File.Exists(path), path);
            Assert.That(File.ReadAllText(path), Does.Contain(UrpUnlitGuid),
                "add Universal Render Pipeline/Unlit to Always Included Shaders");
        }
    }
}
