using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bindrune.Config;
using Bindrune.Portals;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Bindrune.UI
{
    /// <summary>
    /// Picking where a portal points, on the world map and in the list beside it.
    /// <para>
    /// There is one highlighted destination and two renderings of it (DESIGN.md §5). The map answers
    /// <em>where is it</em>; the list answers <em>what are my options, in an order I chose</em>, and
    /// stays useful when the destination is off the visible map or has no name worth recognising.
    /// </para>
    /// <para>
    /// That both views come almost free is the payoff for making selection a <em>highlight</em> the
    /// mouse and the stick both move, rather than a click target with keyboard support bolted on. A
    /// second view of one highlight costs a rendering loop; two independent selections would have
    /// cost a reconciliation problem. It is also why the gamepad works without a single Unity UI
    /// navigation component — nothing has focus, so nothing has to be told where focus goes next.
    /// </para>
    /// <para>
    /// Confirming is always a separate keypress from highlighting, including with the mouse. A
    /// re-aim changes everyone's route — possibly someone's mid-haul — so a stray click on a map
    /// should not be able to do it.
    /// </para>
    /// </summary>
    internal static class DestinationSelector
    {
        private static readonly List<PortalRecord> Candidates = new List<PortalRecord>();
        private static readonly List<Minimap.PinData> Pins = new List<Minimap.PinData>();

        /// <summary>
        /// How the list is ordered. Favourites are the third one §5 asks for; they need somewhere
        /// per-player to persist, so they wait until there is a reason to build that.
        /// </summary>
        private enum SortOrder
        {
            Distance,
            Name,
        }

        /// <summary>
        /// How many destinations the list shows at once. The window scrolls to keep the highlight in
        /// view rather than paging, so the entries either side stay visible and moving through the
        /// list feels continuous.
        /// </summary>
        private const int VisibleRows = 9;

        private static ZDOID _sourceId;
        private static Vector3 _sourcePosition;
        private static string _sourceName;
        private static SortOrder _order = SortOrder.Distance;
        private static int _highlight;
        private static GameObject _panel;
        private static Text _text;
        private static bool _updateSeen;

        internal static bool IsOpen { get; private set; }

        /// <summary>
        /// Opens the selector for a portal the player is standing at. Refuses, with a reason, rather
        /// than opening an empty map.
        /// </summary>
        internal static void Open(TeleportWorld portal, Humanoid who)
        {
            ZDO source = PortalTarget.ZdoOf(portal);
            if (source == null || Minimap.instance == null)
            {
                return;
            }

            if (!ReaimGuard.MayReaim(portal.transform.position, out string refusal))
            {
                who?.Message(MessageHud.MessageType.Center, refusal);
                return;
            }

            _sourceId = source.m_uid;
            long sourcePid = PortalTarget.GetPid(source);
            string tag = source.GetString(ZDOVars.s_tag, string.Empty);
            _sourceName = string.IsNullOrEmpty(tag) ? "this portal" : $"\"{tag}\"";

            _sourcePosition = portal.transform.position;
            Candidates.Clear();
            Candidates.AddRange(PortalRegistry.All.Where(p => p.Pid != sourcePid));
            Sort();

            if (Candidates.Count == 0)
            {
                who?.Message(MessageHud.MessageType.Center, "There is nowhere else to point this portal.");
                return;
            }

            // Start on wherever it already points, so re-opening the selector shows you the current
            // answer instead of making you find it.
            long current = PortalTarget.GetDestination(source);
            _highlight = Candidates.FindIndex(p => p.Pid == current);
            if (_highlight < 0)
            {
                _highlight = 0;
            }

            AddPins();
            BuildPanel();

            IsOpen = true;
            _updateSeen = false;
            Minimap.instance.SetMapMode(Minimap.MapMode.Large);
            ShowHighlight();

            Jotunn.Logger.LogInfo($"Selector opened for {_sourceName} with {Candidates.Count} destination(s).");
        }

        /// <summary>Driven from the <c>Minimap.Update</c> postfix, so it stops when the map does.</summary>
        internal static void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            if (!_updateSeen)
            {
                // Says out loud that the selector is being driven at all. Whether this line appears
                // separates "the keys are not reaching us" from "nothing is running", and guessing
                // between those two cost several rounds of testing.
                _updateSeen = true;
                Jotunn.Logger.LogInfo("Selector is receiving frames.");
            }

            // The player closed the map out from under us — Escape, the map key, anything. Treat it
            // as a cancel rather than leaving a selector running behind a closed map.
            //
            // m_mode, not m_mapLarge: SetMapMode toggles m_largeRoot and leaves m_mapLarge alone, so
            // watching that one meant this never fired and the panel could not be dismissed at all.
            if (Minimap.instance == null || Minimap.instance.m_mode != Minimap.MapMode.Large)
            {
                Close();
                return;
            }

            if (Cancelled())
            {
                Close();
                return;
            }

            if (Confirmed())
            {
                Commit();
                return;
            }

            if (SelectorKeys.Pressed(SelectorKeys.Sort))
            {
                _order = _order == SortOrder.Distance ? SortOrder.Name : SortOrder.Distance;

                // Re-sorting must not move the selection. Reordering the list under a fixed index
                // would silently highlight a different portal, which is the sort of thing that ends
                // with someone re-aiming a portal they did not mean to.
                long held = Candidates[_highlight].Pid;
                Sort();
                _highlight = Mathf.Max(0, Candidates.FindIndex(p => p.Pid == held));
                ShowHighlight();
                return;
            }

            int step = Stepped();
            if (step != 0)
            {
                // Wraps, because a list you can fall off the end of is worse than one you can loop.
                _highlight = (_highlight + step + Candidates.Count) % Candidates.Count;
                ShowHighlight();
            }
        }

        private static void Sort()
        {
            if (_order == SortOrder.Name)
            {
                Candidates.Sort((a, b) => string.Compare(Describe(a), Describe(b), StringComparison.CurrentCultureIgnoreCase));
                return;
            }

            Candidates.Sort((a, b) => Vector3.Distance(_sourcePosition, a.Position)
                .CompareTo(Vector3.Distance(_sourcePosition, b.Position)));
        }

        /// <summary>
        /// A click on the map moves the highlight to the nearest destination. It deliberately does
        /// not confirm — see the note on the class.
        /// </summary>
        internal static void HighlightNearest(Vector3 worldPoint)
        {
            if (!IsOpen || Candidates.Count == 0)
            {
                return;
            }

            int nearest = 0;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < Candidates.Count; i++)
            {
                float distance = (Candidates[i].Position - worldPoint).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }

            _highlight = nearest;
            ShowHighlight();
        }

        private static void Commit()
        {
            ZDO source = ZDOMan.instance?.GetZDO(_sourceId);
            if (source == null)
            {
                // The portal was destroyed while its own selector was open. Rare, but free to handle.
                Close();
                return;
            }

            PortalRecord destination = Candidates[_highlight];
            PortalTarget.Set(source, destination.Pid);

            Player.m_localPlayer?.Message(
                MessageHud.MessageType.Center,
                $"{_sourceName} now points at {Describe(destination)}.");

            Close();
        }

        /// <summary>
        /// Tears the selector down unconditionally, for the world ending underneath it. Without this
        /// a panel could outlive the world it belongs to and follow the player to the main menu.
        /// </summary>
        internal static void Reset() => Close();

        private static void Close()
        {
            IsOpen = false;
            RemovePins();

            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
                _panel = null;
                _text = null;
            }

            Candidates.Clear();

            if (Minimap.instance != null && Minimap.instance.m_mapLarge != null &&
                Minimap.instance.m_mapLarge.activeSelf)
            {
                Minimap.instance.SetMapMode(Minimap.MapMode.Small);
            }
        }

        // -- Presentation ------------------------------------------------------------------------

        private static void AddPins()
        {
            RemovePins();

            foreach (PortalRecord portal in Candidates)
            {
                Minimap.PinData pin = Minimap.instance.AddPin(
                    portal.Position,
                    Minimap.PinType.Icon3,
                    Describe(portal),
                    save: false,
                    isChecked: false,
                    ownerID: 0L,
                    author: default);

                Pins.Add(pin);
            }
        }

        private static void RemovePins()
        {
            foreach (Minimap.PinData pin in Pins)
            {
                if (pin != null)
                {
                    Minimap.instance?.RemovePin(pin);
                }
            }

            Pins.Clear();
        }

        private static void ShowHighlight()
        {
            if (Candidates.Count == 0)
            {
                return;
            }

            PortalRecord destination = Candidates[_highlight];

            // Only on change, so the map still pans under the player's own hand between steps.
            Minimap.instance?.ShowPointOnMap(destination.Position);

            if (_text == null)
            {
                return;
            }

            var panel = new StringBuilder();
            panel.AppendLine($"Aim {_sourceName} at");
            panel.AppendLine($"<size=13>ordered by {(_order == SortOrder.Distance ? "distance" : "name")}</size>");
            panel.AppendLine();

            // A window onto the list rather than the whole thing: clamped so it never runs off
            // either end, and shifted to keep the highlight inside it.
            int first = Mathf.Clamp(_highlight - VisibleRows / 2, 0, Mathf.Max(0, Candidates.Count - VisibleRows));
            int last = Mathf.Min(first + VisibleRows, Candidates.Count);

            panel.AppendLine(first > 0 ? $"<size=13>{first} more above</size>" : " ");

            for (int i = first; i < last; i++)
            {
                PortalRecord row = Candidates[i];
                float distance = Vector3.Distance(_sourcePosition, row.Position);
                string line = $"{Describe(row)}  <size=13>{distance:F0}m</size>";

                panel.AppendLine(i == _highlight
                    ? $"<color=#FFB726>» {line}</color>"
                    : $"<color=#C9C0AC>   {line}</color>");
            }

            panel.AppendLine(last < Candidates.Count ? $"<size=13>{Candidates.Count - last} more below</size>" : " ");
            panel.AppendLine();
            panel.AppendLine($"<size=13>[{Bound(SelectorKeys.Previous)} / {Bound(SelectorKeys.Next)}] change   " +
                             $"[{Bound(SelectorKeys.Sort)}] sort</size>");
            panel.Append($"<size=13>[{Bound(SelectorKeys.Confirm)}] confirm   " +
                         $"[{Bound(SelectorKeys.Cancel)}] cancel</size>");

            _text.text = panel.ToString();
        }

        /// <summary>
        /// Reads the binding back out rather than hardcoding the prompt, so rebinding a key does not
        /// leave the panel telling you to press something else.
        /// </summary>
        private static string Bound(string button) => SelectorKeys.KeyLabel(button);

        /// <summary>
        /// Honours <c>HidePortalNames</c>, which is why nothing formats a portal name itself.
        /// </summary>
        private static string Describe(PortalRecord portal)
        {
            if (BindruneConfig.HidePortalNames.Value)
            {
                return $"portal at {portal.Position.x:F0}, {portal.Position.z:F0}";
            }

            return string.IsNullOrEmpty(portal.Name) ? "unnamed portal" : portal.Name;
        }

        private static void BuildPanel()
        {
            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
            }

            _panel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(-600f, -60f),
                width: 380f,
                height: 470f);

            GameObject text = GUIManager.Instance.CreateText(
                text: string.Empty,
                parent: _panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: Vector2.zero,
                font: GUIManager.Instance.AveriaSerifBold,
                fontSize: 16,
                color: GUIManager.Instance.ValheimOrange,
                outline: true,
                outlineColor: Color.black,
                width: 340f,
                height: 430f,
                addContentSizeFitter: false);

            _text = text.GetComponent<Text>();
            _text.alignment = TextAnchor.UpperLeft;
            _text.supportRichText = true;
        }

        // -- Input -------------------------------------------------------------------------------
        //
        // Every key is a registered, rebindable button rather than a raw read — see SelectorKeys for
        // why. One name covers both keyboard and gamepad, so the two inputs cannot drift apart.

        private static int Stepped()
        {
            if (SelectorKeys.Pressed(SelectorKeys.Next))
            {
                return 1;
            }

            return SelectorKeys.Pressed(SelectorKeys.Previous) ? -1 : 0;
        }

        private static bool Confirmed() => SelectorKeys.Pressed(SelectorKeys.Confirm);

        private static bool Cancelled() => SelectorKeys.Pressed(SelectorKeys.Cancel);
    }
}
