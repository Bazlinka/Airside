using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// The persistent HUD shell (ADR 0057): the top bar, the workspace tabs and the
    /// surface each page is laid out on. UnityEngine-free, so the fits-and-never-overlaps
    /// contract is checked here rather than by eye on a Mac build.
    /// </summary>
    public sealed class HudShellTests
    {
        [Test]
        public void Shell_IsIdenticalOnEveryPage()
        {
            foreach (var (width, height) in HudTestAirline.Viewports)
            {
                var bar = HudShell.TopBar(width, height);
                var nav = HudShell.NavStrip(width, height);
                Assert.That(bar.X, Is.EqualTo(0f), $"{width}x{height}");
                Assert.That(bar.Width, Is.EqualTo(width), $"{width}x{height}");
                Assert.That(nav.Right, Is.EqualTo(width).Within(0.01f), $"{width}x{height}");
                Assert.That(nav.Width, Is.LessThanOrEqualTo(width * 0.5f + 0.01f), $"{width}x{height}");

                foreach (var workspace in new[]
                         {
                             HudWorkspace.None, HudWorkspace.Operations, HudWorkspace.Map,
                             HudWorkspace.Fleet, HudWorkspace.Contracts, HudWorkspace.Stats
                         })
                {
                    var tabs = new List<HudNavTab>();
                    HudShell.FillTabs(nav, workspace, tabs);
                    Assert.That(tabs, Has.Count.EqualTo(HudShell.Tabs.Length), $"{workspace} {width}x{height}");
                    Assert.That(tabs.Count(t => t.Selected), Is.EqualTo(1),
                        $"exactly one tab reads as the open page ({workspace})");
                    for (var i = 1; i < tabs.Count; i++)
                        Assert.That(tabs[i].Box.X, Is.GreaterThanOrEqualTo(tabs[i - 1].Box.Right - 0.01f),
                            "tabs never overlap");
                }
            }
        }

        [Test]
        public void Shell_DropsTopBarValuesRatherThanRunningThemUnderTheTabs()
        {
            var values = new[] { "ADELAIDE  12:36", "$12,480", "RELIABILITY 97%", "REGIONAL" };
            foreach (var (width, height) in HudTestAirline.Viewports)
            {
                var bar = HudShell.TopBar(width, height);
                var nav = HudShell.NavStrip(width, height);
                var segments = new List<HudTopBarSegment>();
                HudShell.FillSegments(bar, "A Very Long Airline Name Indeed", nav, values, segments);
                foreach (var segment in segments)
                    Assert.That(segment.Box.Right, Is.LessThanOrEqualTo(nav.X + 0.01f),
                        $"{width}x{height}: a value ran under the workspace tabs");
            }
        }

        [Test]
        public void Shell_KeepsTheObjectiveCardBesideTheWorkspaceWhenThereIsRoom()
        {
            var workspace = HudShell.WorkspaceSurface(1440f, 900f);
            var objective = HudShell.Objective(1440f, 900f, showGuide: false);
            Assert.That(HudShell.ObjectiveSurvivesWorkspace(1440f, 900f), Is.True);
            Assert.That(workspace.Overlaps(objective), Is.False);
            Assert.That(workspace.X, Is.GreaterThan(objective.Right));
            Assert.That(workspace.Right, Is.LessThanOrEqualTo(1440f - HudShell.Margin + 0.01f));
        }

        [Test]
        public void Shell_GivesTheWholeWidthToAWorkspaceTooNarrowToShareIt()
        {
            var workspace = HudShell.WorkspaceSurface(820f, 600f);
            Assert.That(HudShell.ObjectiveSurvivesWorkspace(820f, 600f), Is.False);
            Assert.That(workspace.X, Is.EqualTo(HudShell.Margin));
            Assert.That(workspace.Width, Is.EqualTo(820f - HudShell.Margin * 2f));
        }

        [Test]
        public void Shell_NeverLetsAWorkspaceRunUnderTheTopBarOrOffTheBottom()
        {
            foreach (var (width, height) in HudTestAirline.Viewports)
            {
                var bar = HudShell.TopBar(width, height);
                var workspace = HudShell.WorkspaceSurface(width, height);
                Assert.That(workspace.Y, Is.GreaterThanOrEqualTo(bar.Bottom), $"{width}x{height}");
                Assert.That(workspace.Bottom, Is.LessThanOrEqualTo(height - HudShell.Margin + 0.01f),
                    $"{width}x{height}");
                Assert.That(workspace.Width, Is.GreaterThan(0f), $"{width}x{height}");
            }
        }

    }
}
