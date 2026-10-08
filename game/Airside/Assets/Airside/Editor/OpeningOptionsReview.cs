using System;
using System.IO;
using System.Reflection;
using Airside.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Actual Unity IMGUI review of shared startup/options painters using labelled sample data.</summary>
public sealed class OpeningOptionsReview : EditorWindow
{
    private string _output;
    private int _page;
    private int _size;
    private double _captureAt;
    private bool _drawn;
    private Vector2 _screenOrigin;
    private readonly HudPainter _painter = new();
    private readonly HudDrawList _draw = new();
    private GameObject _host;
    private AirsidePrototype _prototype;
    private static readonly string[] Names = { "fresh-title", "returning-title", "options-general", "options-camera", "options-display", "options-world" };
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Capture()
    {
        var args = Environment.GetCommandLineArgs();
        var output = "work/opening-options-native";
        for (var i = 0; i + 1 < args.Length; i++)
            if (args[i] == "-openingReviewOutput") output = args[i + 1];
        Directory.CreateDirectory(output);
        var window = CreateInstance<OpeningOptionsReview>();
        window._output = output;
        window.titleContent = new GUIContent("Airside native UI review — sample airline");
        window.ShowUtility();
        window.NextView();
        EditorApplication.update += window.Tick;
    }

    private void NextView()
    {
        var width = _size == 0 ? 1024f : 1440f;
        var height = _size == 0 ? 640f : 900f;
        minSize = maxSize = new Vector2(width, height);
        position = new Rect(40f, 50f, width, height);
        Focus();
        _drawn = false;
        _captureAt = EditorApplication.timeSinceStartup + 2.5;
        Repaint();
    }

    private void OnGUI()
    {
        _screenOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero);
        var scale = HudLayout.ScaleFor((int)position.width, (int)position.height);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        var width = position.width / scale;
        var height = position.height / scale;
        var splash = new SplashModel
        {
            HasSave = _page > 0, SaveName = "Southern Cross Regional", SaveTier = "Regional",
            SaveSummary = "4 aircraft  ·  $23,400  ·  91% reliability", SavedWhen = "Saved 8 Oct 12:30  ·  18 trips flown",
            ClockText = "12:30", SaveLiveryHex = "#39708A"
        };
        SplashPainter.Paint(_draw, SplashLayout.Create(width, height, SplashStep.Menu, splash.HasSave), splash);
        _painter.Draw(_draw);
        if (_page >= 2)
        {
            if (_prototype == null)
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(AirsidePrototype).TypeHandle);
                _host = new GameObject("Inactive options review model");
                _host.SetActive(false);
                _prototype = _host.AddComponent<AirsidePrototype>();
            }
            var model = (OptionsMenuModel)typeof(AirsidePrototype).GetField("_optionsModel", Private).GetValue(_prototype);
            model.Section = (OptionsSection)(_page - 2);
            typeof(AirsidePrototype).GetMethod("FillOptionsModel", Private).Invoke(_prototype, null);
            OptionsMenuPainter.Paint(_draw, OptionsMenuPainter.Panel(width, height), model);
            _painter.Draw(_draw);
        }
        if (Event.current.type == EventType.Repaint) _drawn = true;
    }

    private void Tick()
    {
        if (!_drawn || EditorApplication.timeSinceStartup < _captureAt) return;
        try
        {
            var w = (int)position.width;
            var h = (int)position.height;
            var path = Path.Combine(_output, Names[_page] + "-" + w + "x" + h + ".png");
            var capture = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/usr/sbin/screencapture",
                Arguments = "-x -R" + (int)_screenOrigin.x + "," + (int)_screenOrigin.y + "," + w + "," + h + " \"" + path + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var process = System.Diagnostics.Process.Start(capture))
            {
                process.WaitForExit();
                if (process.ExitCode != 0) throw new IOException("Native UI capture failed.");
            }
            if (_page == 0 && _size == 0)
                File.WriteAllText(Path.Combine(_output, "art-diagnostics.txt"),
                    "Splash path: " + ArtRuntimePaths.ResolveExisting(SplashLayout.SplashArt) +
                    "\nSplash loaded: " + (AirsideTheme.SplashDawn != null) +
                    "\nWordmark loaded: " + (AirsideTheme.WordmarkLight != null) + "\n");
            _page++;
            if (_page == Names.Length) { _page = 0; _size++; }
            if (_size == 2)
            {
                EditorApplication.update -= Tick;
                if (_host != null) DestroyImmediate(_host);
                File.WriteAllText(Path.Combine(_output, "COMPLETE.txt"), "12 native Unity IMGUI views; sample airline; no gameplay or saved preferences modified.\n");
                EditorApplication.Exit(0);
                return;
            }
            NextView();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(1);
        }
    }
}
