using System.Reflection;
using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class CameraShellAnchorTests
    {
        private static void PlaceAfterCameraMotion(Camera camera)
        {
            typeof(CameraShellAnchor).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(camera.GetComponent<CameraShellAnchor>(), null);
        }

        [Test]
        public void HandoffKeepsTheLatestCameraAsSoleOwnerInEitherUpdateOrder()
        {
            var oldHost = new GameObject("Old shell camera");
            var newHost = new GameObject("New shell camera");
            var shell = new GameObject("Handed off shell");
            var retained = new GameObject("Old camera retained shell");
            try
            {
                var oldCamera = oldHost.AddComponent<Camera>();
                var newCamera = newHost.AddComponent<Camera>();
                CameraShellAnchor.Place(oldCamera, shell.transform, Vector3.up * 420f);
                CameraShellAnchor.Place(oldCamera, retained.transform, Vector3.right * 7f);
                CameraShellAnchor.Place(newCamera, shell.transform, Vector3.up * 700f, absoluteHeight: true);
                oldHost.transform.position = new Vector3(1f, 2f, 3f);
                newHost.transform.position = new Vector3(60f, 9f, 30f);

                PlaceAfterCameraMotion(newCamera);
                PlaceAfterCameraMotion(oldCamera);
                Assert.That(shell.transform.position, Is.EqualTo(new Vector3(60f, 700f, 30f)));
                Assert.That(retained.transform.position, Is.EqualTo(new Vector3(8f, 2f, 3f)),
                    "removing a handed off entry must retain the other shell's index");

                oldHost.transform.position = new Vector3(-40f, 10f, -70f);
                newHost.transform.position = new Vector3(80f, 19f, 40f);
                PlaceAfterCameraMotion(oldCamera);
                PlaceAfterCameraMotion(newCamera);
                Assert.That(shell.transform.position, Is.EqualTo(new Vector3(80f, 700f, 40f)));
            }
            finally
            {
                Object.DestroyImmediate(shell);
                Object.DestroyImmediate(retained);
                Object.DestroyImmediate(newHost);
                Object.DestroyImmediate(oldHost);
            }
        }

        [Test]
        public void LatePlacementUsesFinalCameraPositionAndDirection()
        {
            var host = new GameObject("Moving shell camera");
            var shell = new GameObject("Rain shell");
            try
            {
                var camera = host.AddComponent<Camera>();
                CameraShellAnchor.Place(camera, shell.transform, Vector3.up * 2f, forwardMetres: 10f);
                host.transform.SetPositionAndRotation(new Vector3(60f, 9f, 30f), Quaternion.Euler(0f, 90f, 0f));
                PlaceAfterCameraMotion(camera);
                Assert.That(Vector3.Distance(shell.transform.position, new Vector3(70f, 11f, 30f)),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(shell);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void DestroyingTheOwningCameraAllowsAnotherCameraToClaimTheShell()
        {
            var oldHost = new GameObject("Destroyed shell camera");
            var newHost = new GameObject("Replacement shell camera");
            var shell = new GameObject("Surviving shell");
            try
            {
                CameraShellAnchor.Place(oldHost.AddComponent<Camera>(), shell.transform, Vector3.up * 420f);
                Object.DestroyImmediate(oldHost);
                var camera = newHost.AddComponent<Camera>();
                CameraShellAnchor.Place(camera, shell.transform, Vector3.up * 10f);
                newHost.transform.position = new Vector3(60f, 9f, 30f);
                PlaceAfterCameraMotion(camera);
                Assert.That(shell.transform.position, Is.EqualTo(new Vector3(60f, 19f, 30f)));
            }
            finally
            {
                Object.DestroyImmediate(shell);
                Object.DestroyImmediate(newHost);
                if (oldHost != null) Object.DestroyImmediate(oldHost);
            }
        }

        [Test]
        public void DestroyedShellDoesNotPreventTheRemainingShellFromFollowing()
        {
            var host = new GameObject("Shell camera");
            var removed = new GameObject("Destroyed shell");
            var shell = new GameObject("Retained shell");
            try
            {
                var camera = host.AddComponent<Camera>();
                CameraShellAnchor.Place(camera, removed.transform, Vector3.zero);
                CameraShellAnchor.Place(camera, shell.transform, Vector3.up * 420f);
                Object.DestroyImmediate(removed);
                host.transform.position = new Vector3(60f, 9f, 30f);
                PlaceAfterCameraMotion(camera);
                Assert.That(shell.transform.position, Is.EqualTo(new Vector3(60f, 429f, 30f)));
                CameraShellAnchor.Place(camera, shell.transform, Vector3.up * 10f);
                PlaceAfterCameraMotion(camera);
                Assert.That(shell.transform.position, Is.EqualTo(new Vector3(60f, 19f, 30f)));
            }
            finally
            {
                if (removed != null) Object.DestroyImmediate(removed);
                Object.DestroyImmediate(shell);
                Object.DestroyImmediate(host);
            }
        }
    }
}
