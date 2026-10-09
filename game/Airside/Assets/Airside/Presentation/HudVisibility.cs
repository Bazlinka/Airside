using System;

namespace Airside.Presentation
{
    /// <summary>The airline HUD's two world views: nothing followed (the field overview) and following an aircraft.</summary>
    public enum HudView
    {
        Overview,
        Follow
    }

    /// <summary>The parts of the airline HUD a player can show or hide per view.</summary>
    public enum HudElement
    {
        AircraftLabels,
        AirportMap,
        FlightTracker,
        CareerCard
    }

    /// <summary>
    /// Which HUD parts show in which view. Each view stores its elements as a bit mask so the settings save is two
    /// integers. Pure, so the rules and the save round trip are checked headlessly.
    /// </summary>
    public sealed class HudVisibility
    {
        public const int ViewCount = 2;
        public const int ElementCount = 4;
        private const int AllMask = (1 << ElementCount) - 1;

        private readonly int[] _masks = new int[ViewCount];

        /// <summary>The out-of-the-box layout: labels, tracker and career card everywhere; the map on request.</summary>
        public static HudVisibility Default(bool labels = true, bool airportMap = false)
        {
            var visibility = new HudVisibility();
            foreach (HudView view in Enum.GetValues(typeof(HudView)))
            {
                visibility.Set(view, HudElement.AircraftLabels, labels);
                visibility.Set(view, HudElement.AirportMap, airportMap);
                visibility.Set(view, HudElement.FlightTracker, true);
                visibility.Set(view, HudElement.CareerCard, true);
            }

            return visibility;
        }

        public bool Shows(HudView view, HudElement element) => (_masks[(int)view] & Bit(element)) != 0;

        public void Set(HudView view, HudElement element, bool shown)
        {
            if (shown)
                _masks[(int)view] |= Bit(element);
            else
                _masks[(int)view] &= ~Bit(element);
        }

        /// <summary>Flips an element in one view and returns its new state.</summary>
        public bool Toggle(HudView view, HudElement element)
        {
            Set(view, element, !Shows(view, element));
            return Shows(view, element);
        }

        public int Mask(HudView view) => _masks[(int)view];

        public void SetMask(HudView view, int mask) => _masks[(int)view] = mask & AllMask;

        public HudVisibility Copy()
        {
            var copy = new HudVisibility();
            Array.Copy(_masks, copy._masks, ViewCount);
            return copy;
        }

        private static int Bit(HudElement element) => 1 << (int)element;

        public static string Label(HudView view) => view == HudView.Follow ? "Following an aircraft" : "Airport overview";

        public static string Label(HudElement element) => element switch
        {
            HudElement.AircraftLabels => "Aircraft labels",
            HudElement.AirportMap => "Airport map",
            HudElement.FlightTracker => "Flight tracker",
            _ => "Career card"
        };

        public static string Detail(HudElement element) => element switch
        {
            HudElement.AircraftLabels => "Registration labels over aircraft on the field.",
            HudElement.AirportMap => "The corner airport map.",
            HudElement.FlightTracker => "Step-by-step progress for every flight you have planned or in the air.",
            _ => "The career progress card in the bottom-left corner (the first-flight guide always shows)."
        };
    }
}
