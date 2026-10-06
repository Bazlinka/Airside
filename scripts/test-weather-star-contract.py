#!/usr/bin/env python3
"""Static producer/shader contracts only: does not compile a shader or render pixels."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / 'game/Airside/Assets/Airside'
PRESENTATION = ASSETS / 'Presentation'


def source(name):
    return (PRESENTATION / name).read_text()


class WeatherStarSourceContracts(unittest.TestCase):
    def test_nav_lens_point_and_initial_material_share_palette(self):
        lights = source('AirsidePrototype.Lights.cs')
        self.assertIn('ToColor(AircraftNavigationPalette.For(kind))', lights)
        self.assertIn('light.color = NavLensColor(kind)', lights)
        initial = source('AirsidePrototype.AircraftVisuals.cs')
        for side, kind in [('left', 'Left'), ('right', 'Right')]:
            self.assertIn(f'"nav_light_{side}" => NavLensColor(AircraftNavigationLight.{kind})', initial)
        self.assertIn('"tail_nav_light" => NavLensColor(AircraftNavigationLight.Tail)', initial)

    def test_cockpit_and_rain_consume_same_observer_input(self):
        cockpit = source('AirsidePrototype.Cockpit.cs')
        rain = source('AirsidePrototype.WeatherEffects.cs')
        self.assertIn('CockpitObserverWeather.Rain(CurrentWeatherLook.Precipitation', cockpit)
        self.assertIn('SetEnvironment(PresentationDaylight, observerRain', cockpit)
        self.assertIn('precipitation = CockpitObserverWeather.Rain(precipitation', rain)
        for producer in [cockpit, rain]:
            self.assertIn('_cockpitView.position.y', producer)
            self.assertIn('InCockpit && _cockpitView != null', producer)
        self.assertIn('CockpitObserverWeather.WipersActive(precipitation)', source('CockpitInterior.cs'))

    def test_all_wind_producers_consume_flow_builders(self):
        for filename, call in [('AirsidePrototype.Sky.cs', 'Cloud'),
                               ('AirsidePrototype.WeatherEffects.cs', 'Rain'),
                               ('AirsidePrototype.Atmosphere.cs', 'ShaderGlobal')]:
            code = source(filename)
            self.assertIn('WeatherWindFlow.' + call + '(PresentationWind', code)
            self.assertNotIn('UnityYawFromTrue(PresentationWind.DirectionDegrees)', code)
            self.assertIn('flow.X', code)
            self.assertIn('flow.Z', code)

    def test_vertex_colours_reach_fragment_and_global_fade(self):
        shader = (ASSETS / 'Art/Shaders/CelestialStars.shader').read_text()
        self.assertIn('half4 color : COLOR', shader)
        self.assertIn('output.color = input.color', shader)
        self.assertIn('return input.color * _BaseColor', shader)
        sky = source('AirsidePrototype.Sky.cs')
        self.assertIn('ToColor(CelestialStarColour.For(bright, tint))', sky)
        self.assertIn('mesh.SetColors(colors)', sky)
        self.assertIn('var c = new Color(twinkle, twinkle, 1f) * fade', sky)
        self.assertIn('Blend One One', shader)  # zero RGB fade adds zero light; never replaces the sky
        self.assertNotIn('c.a = 1f', sky)

    def test_stars_are_background_and_do_not_write_scene_depth(self):
        shader = (ASSETS / 'Art/Shaders/CelestialStars.shader').read_text()
        for contract in ['"Queue"="Background"', 'ZWrite Off', 'ZTest LEqual',
                         'Blend One One',
                         'output.positionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.positionCS.w']:
            self.assertIn(contract, shader)
        self.assertNotIn('DepthOnly', shader)
        self.assertNotIn('DepthNormals', shader)

    def test_dedicated_material_and_packaged_shader_inclusion(self):
        materials = source('AirsideMaterialLibrary.cs')
        self.assertIn('Shader.Find("Airside/CelestialStars")', materials)
        self.assertIn('CreateSharedStars()', source('AirsidePrototype.Sky.cs'))
        self.assertIn('_starsMaterial != null', materials)
        self.assertIn('shader != null && shader.isSupported', materials)
        self.assertIn('Create(Color.white, SurfaceKind.UnlitSky, useTextures: false)', materials)
        self.assertIn('_starsMaterial.SetColor("_BaseColor", Color.white)', materials)
        guid = re.search(r'guid: ([a-f0-9]{32})', (ASSETS / 'Art/Shaders/CelestialStars.shader.meta').read_text()).group(1)
        self.assertIn('guid: ' + guid, (ROOT / 'game/Airside/ProjectSettings/GraphicsSettings.asset').read_text())


if __name__ == '__main__':
    unittest.main()
